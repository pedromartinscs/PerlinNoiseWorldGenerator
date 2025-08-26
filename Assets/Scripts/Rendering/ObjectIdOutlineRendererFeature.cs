using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class ObjectIdOutlineRenderGraphFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public string profilerTag = "ObjectIdOutlineRG";
        public LayerMask outlineLayer = ~0; // set to your Outline layer
        public RenderPassEvent injection = RenderPassEvent.AfterRenderingOpaques;

        [Header("Materials")]
        public Material idWriteMaterial;      // Hidden/Outline/ObjectIdWrite
        public Material compositeMaterial;    // Hidden/Outline/Composite

        [Header("Composite Params")]
        [Range(1, 6)] public int thickness = 2;
        [Range(0.0001f, 0.01f)] public float depthThreshold = 0.002f;
        public Color outlineColor = Color.black;
        public bool drawInnerLines = true;
    }

    class CombinedPass : ScriptableRenderPass
    {
        readonly Settings settings;

        static readonly ShaderTagId[] k_ShaderTags = new[]
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit"),
        };

        // Pass data must be reference types for RG
        class IdPassData
        {
            public RendererListHandle rendererList;
            public TextureHandle idTex;
            public TextureHandle cameraDepth;
        }

        class CompositePassData
        {
            public TextureHandle idTex;
            public TextureHandle cameraColor;
            public TextureHandle cameraDepth;
            public Material compositeMat;
            public int thickness;
            public float depthThreshold;
            public Vector4 idTexelSize;
            public Color outlineColor;
            public float drawInnerLines;
        }

        public CombinedPass(Settings s)
        {
            settings = s;
            renderPassEvent = s.injection;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var res      = frameData.Get<UniversalResourceData>();   // cameraColor, cameraDepth, etc.
            var urData   = frameData.Get<UniversalRenderingData>();  // culling results
            var camData  = frameData.Get<UniversalCameraData>();     // camera info
            var camera   = camData.camera;

            // -------- 1) Build renderer list that overrides material to ID-writer
            var sorting = new SortingSettings(camera)
            { criteria = SortingCriteria.CommonOpaque | SortingCriteria.RenderQueue | SortingCriteria.QuantizedFrontToBack };

            var draw = new DrawingSettings(k_ShaderTags[0], sorting);
            for (int i = 1; i < k_ShaderTags.Length; i++) draw.SetShaderPassName(i, k_ShaderTags[i]);
            draw.overrideMaterial          = settings.idWriteMaterial;
            draw.overrideMaterialPassIndex = 0;

            var filter   = new FilteringSettings(RenderQueueRange.all, settings.outlineLayer);
            var rlParams = new RendererListParams(urData.cullResults, draw, filter);
            var rlHandle = renderGraph.CreateRendererList(rlParams);

            // -------- 2) Allocate R8 ID texture
            var cameraDesc = res.cameraColor.GetDescriptor(renderGraph);
			cameraDesc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm;
			cameraDesc.depthBufferBits = 0;
			var idTex = renderGraph.CreateTexture(new TextureDesc(cameraDesc) { name = "_ObjectIdTex" });

            // -------- 3) Raster pass to write object IDs with depth test
            using (var builder = renderGraph.AddRasterRenderPass<IdPassData>(settings.profilerTag + "_IDs", out var passData))
            {
                passData.rendererList = rlHandle;
                passData.idTex        = idTex;
                passData.cameraDepth  = res.cameraDepth;

                builder.UseRendererList(rlHandle);
                builder.SetRenderAttachment(idTex, 0, AccessFlags.WriteAll);           // write IDs
                builder.SetRenderAttachmentDepth(res.cameraDepth, AccessFlags.Read);   // depth test read

                builder.SetRenderFunc((IdPassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.ClearRenderTarget(clearDepth: false, clearColor: true, backgroundColor: Color.black);
                    ctx.cmd.DrawRendererList(data.rendererList);
                });
            }

            // -------- 4) Fullscreen composite over camera color
            int w = Mathf.Max(1, camera.pixelWidth);
            int h = Mathf.Max(1, camera.pixelHeight);
            var texelSize = new Vector4(1f / w, 1f / h, w, h);

            using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(settings.profilerTag + "_Composite", out var passData))
            {
                passData.idTex          = idTex;
                passData.cameraColor    = res.cameraColor;
                passData.cameraDepth    = res.cameraDepth;
                passData.compositeMat   = settings.compositeMaterial;
                passData.thickness      = settings.thickness;
                passData.depthThreshold = settings.depthThreshold;
                passData.idTexelSize    = texelSize;
                passData.outlineColor   = settings.outlineColor;
                passData.drawInnerLines = settings.drawInnerLines ? 1f : 0f;

                // Declare resource usage so RG tracks dependencies
                builder.UseTexture(idTex, AccessFlags.Read);
				builder.UseTexture(res.cameraDepth, AccessFlags.Read); // depth texture
				builder.SetRenderAttachment(res.cameraColor, 0, AccessFlags.ReadWrite);
				
				builder.SetRenderFunc((CompositePassData data, RasterGraphContext ctx) =>
				{
					var mpb = new MaterialPropertyBlock();
					mpb.SetTexture("_ObjectIdTex", (Texture)data.idTex); // implicit cast from TextureHandle
					mpb.SetVector("_ObjectIdTex_TexelSize", data.idTexelSize);
					mpb.SetTexture("_DepthTex", (Texture)data.cameraDepth);
				
					mpb.SetColor("_OutlineColor",   data.outlineColor);
					mpb.SetFloat("_Thickness",      data.thickness);
					mpb.SetFloat("_DepthThreshold", data.depthThreshold);
					mpb.SetFloat("_DrawInnerLines", data.drawInnerLines);
					
					// mpb.SetFloat("_DebugId", 1f);
					CoreUtils.DrawFullScreen(ctx.cmd, data.compositeMat, mpb);
				});
            }
        }
    }

    public Settings settings = new Settings();
    CombinedPass _pass;

    public override void Create()
    {
        _pass = new CombinedPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.idWriteMaterial == null || settings.compositeMaterial == null)
            return;

        renderer.EnqueuePass(_pass);
    }
}
