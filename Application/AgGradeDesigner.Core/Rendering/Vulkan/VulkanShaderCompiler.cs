using System.Runtime.InteropServices;

using Silk.NET.Shaderc;

namespace AgGradeDesigner.Core.Rendering.Vulkan
{
    /// <summary>
    /// Compiles embedded GLSL sources to SPIR-V via Shaderc.
    /// </summary>
    static internal unsafe class VulkanShaderCompiler
    {
        /// <summary>Embedded GLSL source for the coloured mesh and line vertex shader.</summary>
        private const string FieldVertexShaderGlsl = """
            #version 450

            layout(location = 0) in vec3 inPosition;
            layout(location = 1) in vec4 inColor;

            layout(set = 0, binding = 0) uniform CameraUBO {
                vec2 viewportSize;
                vec2 viewportMid;
                float scale;
                float cosH;
                float sinH;
                float cosP;
                float sinP;
                float depthMin;
                float depthMax;
                float pivotEastM;
                float pivotNorthM;
            } cam;

            layout(location = 0) out vec4 fragColor;

            void main() {
                float Ex = inPosition.x + cam.pivotEastM;
                float Ey = inPosition.y + cam.pivotNorthM;
                float Ez = inPosition.z;

                float Rx = Ex * cam.cosH - Ey * cam.sinH;
                float Fy = Ex * cam.sinH + Ey * cam.cosH;
                float depth = Fy * cam.cosP + Ez * cam.sinP;
                float Up = Ez * cam.cosP - Fy * cam.sinP;

                float screenX = cam.viewportMid.x - Rx * cam.scale;
                float screenY = cam.viewportMid.y - Up * cam.scale;

                float ndcX = (screenX / cam.viewportSize.x) * 2.0 - 1.0;
                // Vulkan clip space: Y points down (top = -1, bottom = +1).
                float ndcY = (screenY / cam.viewportSize.y) * 2.0 - 1.0;

                float depthRange = max(cam.depthMax - cam.depthMin, 1.0e-6);
                float normalizedDepth = clamp((depth - cam.depthMin) / depthRange, 0.0, 1.0);
                // Vulkan depth: 0 = near, 1 = far; larger camera depth is closer.
                float ndcZ = 1.0 - normalizedDepth;

                gl_Position = vec4(ndcX, ndcY, ndcZ, 1.0);
                fragColor = inColor;
            }
            """;

        /// <summary>Embedded GLSL source for the opaque coloured fragment shader.</summary>
        private const string FieldFragmentShaderGlsl = """
            #version 450

            layout(location = 0) in vec4 fragColor;
            layout(location = 0) out vec4 outColor;

            void main() {
                outColor = fragColor;
            }
            """;

        /// <summary>Embedded GLSL source for the alpha-modulated overlay fragment shader.</summary>
        private const string FieldOverlayFragmentShaderGlsl = """
            #version 450

            layout(location = 0) in vec4 fragColor;
            layout(location = 0) out vec4 outColor;

            layout(push_constant) uniform Push {
                float overlayOpacity;
            } push;

            void main() {
                outColor = vec4(fragColor.rgb, fragColor.a * push.overlayOpacity);
            }
            """;

        /// <summary>
        /// Compiles the field vertex shader to SPIR-V.
        /// </summary>
        /// <param name="Error">Receives the Shaderc error message when compilation fails.</param>
        /// <returns>SPIR-V bytecode, or an empty array on failure.</returns>
        static public byte[] CompileVertexShader
            (
            out string? Error
            )
        {
            return Compile(FieldVertexShaderGlsl, ShaderKind.VertexShader, "field.vert", out Error);
        }

        /// <summary>
        /// Compiles the opaque field fragment shader to SPIR-V.
        /// </summary>
        /// <param name="Error">Receives the Shaderc error message when compilation fails.</param>
        /// <returns>SPIR-V bytecode, or an empty array on failure.</returns>
        static public byte[] CompileFragmentShader
            (
            out string? Error
            )
        {
            return Compile(FieldFragmentShaderGlsl, ShaderKind.FragmentShader, "field.frag", out Error);
        }

        /// <summary>
        /// Compiles the overlay fragment shader with push-constant opacity to SPIR-V.
        /// </summary>
        /// <param name="Error">Receives the Shaderc error message when compilation fails.</param>
        /// <returns>SPIR-V bytecode, or an empty array on failure.</returns>
        static public byte[] CompileOverlayFragmentShader
            (
            out string? Error
            )
        {
            return Compile(FieldOverlayFragmentShaderGlsl, ShaderKind.FragmentShader, "field_overlay.frag", out Error);
        }

        /// <summary>
        /// Compiles the textured satellite tile vertex shader to SPIR-V.
        /// </summary>
        /// <param name="Error">Receives the Shaderc error message when compilation fails.</param>
        /// <returns>SPIR-V bytecode, or an empty array on failure.</returns>
        static public byte[] CompileTexturedVertexShader
            (
            out string? Error
            )
        {
            return Compile(TexturedVertexShaderGlsl, ShaderKind.VertexShader, "field_tex.vert", out Error);
        }

        /// <summary>
        /// Compiles the textured satellite tile fragment shader to SPIR-V.
        /// </summary>
        /// <param name="Error">Receives the Shaderc error message when compilation fails.</param>
        /// <returns>SPIR-V bytecode, or an empty array on failure.</returns>
        static public byte[] CompileTexturedFragmentShader
            (
            out string? Error
            )
        {
            return Compile(TexturedFragmentShaderGlsl, ShaderKind.FragmentShader, "field_tex.frag", out Error);
        }

        /// <summary>
        /// Compiles the 2D plan-view coloured vertex shader to SPIR-V.
        /// </summary>
        static public byte[] CompilePlanViewColoredVertexShader
            (
            out string? Error
            )
        {
            return Compile(PlanViewColoredVertexShaderGlsl, ShaderKind.VertexShader, "plan_colored.vert", out Error);
        }

        /// <summary>
        /// Compiles the 2D plan-view textured vertex shader to SPIR-V.
        /// </summary>
        static public byte[] CompilePlanViewTexturedVertexShader
            (
            out string? Error
            )
        {
            return Compile(PlanViewTexturedVertexShaderGlsl, ShaderKind.VertexShader, "plan_tex.vert", out Error);
        }

        /// <summary>Embedded GLSL source for the textured satellite tile vertex shader.</summary>
        private static readonly string TexturedVertexShaderGlsl = """
            #version 450

            layout(location = 0) in vec3 inPosition;
            layout(location = 1) in vec2 inTexCoord;

            layout(set = 0, binding = 0) uniform CameraUBO {
                vec2 viewportSize;
                vec2 viewportMid;
                float scale;
                float cosH;
                float sinH;
                float cosP;
                float sinP;
                float depthMin;
                float depthMax;
                float pivotEastM;
                float pivotNorthM;
            } cam;

            layout(location = 0) out vec2 fragTexCoord;

            void main() {
                float Ex = inPosition.x + cam.pivotEastM;
                float Ey = inPosition.y + cam.pivotNorthM;
                float Ez = inPosition.z;

                float Rx = Ex * cam.cosH - Ey * cam.sinH;
                float Fy = Ex * cam.sinH + Ey * cam.cosH;
                float depth = Fy * cam.cosP + Ez * cam.sinP;
                float Up = Ez * cam.cosP - Fy * cam.sinP;

                float screenX = cam.viewportMid.x - Rx * cam.scale;
                float screenY = cam.viewportMid.y - Up * cam.scale;

                float ndcX = (screenX / cam.viewportSize.x) * 2.0 - 1.0;
                float ndcY = (screenY / cam.viewportSize.y) * 2.0 - 1.0;

                float depthRange = max(cam.depthMax - cam.depthMin, 1.0e-6);
                float normalizedDepth = clamp((depth - cam.depthMin) / depthRange, 0.0, 1.0);
                float ndcZ = 1.0 - normalizedDepth;

                gl_Position = vec4(ndcX, ndcY, ndcZ, 1.0);
                fragTexCoord = inTexCoord;
            }
            """;

        /// <summary>Embedded GLSL source for the textured satellite tile fragment shader.</summary>
        private const string TexturedFragmentShaderGlsl = """
            #version 450

            layout(location = 0) in vec2 fragTexCoord;
            layout(set = 0, binding = 1) uniform sampler2D tileSampler;
            layout(location = 0) out vec4 outColor;

            void main() {
                outColor = texture(tileSampler, fragTexCoord);
            }
            """;

        /// <summary>Embedded GLSL for 2D plan-view coloured geometry (screen pixel coordinates).</summary>
        private const string PlanViewColoredVertexShaderGlsl = """
            #version 450

            layout(location = 0) in vec3 inScreenPos;
            layout(location = 1) in vec4 inColor;

            layout(set = 0, binding = 0) uniform CameraUBO {
                vec2 viewportSize;
                vec2 viewportMid;
                float scale;
                float cosH;
                float sinH;
                float cosP;
                float sinP;
                float depthMin;
                float depthMax;
                float pivotEastM;
                float pivotNorthM;
            } cam;

            layout(location = 0) out vec4 fragColor;

            void main() {
                float ndcX = (inScreenPos.x / cam.viewportSize.x) * 2.0 - 1.0;
                float ndcY = (inScreenPos.y / cam.viewportSize.y) * 2.0 - 1.0;
                float ndcZ = 1.0 - clamp(inScreenPos.z, 0.0, 1.0);
                gl_Position = vec4(ndcX, ndcY, ndcZ, 1.0);
                fragColor = inColor;
            }
            """;

        /// <summary>Embedded GLSL for 2D plan-view textured geometry (screen pixel coordinates).</summary>
        private const string PlanViewTexturedVertexShaderGlsl = """
            #version 450

            layout(location = 0) in vec3 inScreenPos;
            layout(location = 1) in vec2 inTexCoord;

            layout(set = 0, binding = 0) uniform CameraUBO {
                vec2 viewportSize;
                vec2 viewportMid;
                float scale;
                float cosH;
                float sinH;
                float cosP;
                float sinP;
                float depthMin;
                float depthMax;
                float pivotEastM;
                float pivotNorthM;
            } cam;

            layout(location = 0) out vec2 fragTexCoord;

            void main() {
                float ndcX = (inScreenPos.x / cam.viewportSize.x) * 2.0 - 1.0;
                float ndcY = (inScreenPos.y / cam.viewportSize.y) * 2.0 - 1.0;
                float ndcZ = 1.0 - clamp(inScreenPos.z, 0.0, 1.0);
                gl_Position = vec4(ndcX, ndcY, ndcZ, 1.0);
                fragTexCoord = inTexCoord;
            }
            """;

        /// <summary>
        /// Compiles one GLSL source string to SPIR-V via Shaderc.
        /// </summary>
        /// <param name="Source">GLSL source text.</param>
        /// <param name="Kind">Shader stage to compile.</param>
        /// <param name="Name">Debug name passed to Shaderc.</param>
        /// <param name="Error">Receives the Shaderc error message when compilation fails.</param>
        /// <returns>SPIR-V bytecode, or an empty array on failure.</returns>
        static private byte[] Compile
            (
            string Source,
            ShaderKind Kind,
            string Name,
            out string? Error
            )
        {
            Error = null;
            Shaderc ShadercApi = Shaderc.GetApi();
            Compiler* Compiler = ShadercApi.CompilerInitialize();
            CompileOptions* Options = ShadercApi.CompileOptionsInitialize();
            try
            {
                ShadercApi.CompileOptionsSetOptimizationLevel(Options, OptimizationLevel.Performance);
                byte[] SourceBytes = System.Text.Encoding.UTF8.GetBytes(Source);
                fixed (byte* SourcePtr = SourceBytes)
                {
                    CompilationResult* Result = ShadercApi.CompileIntoSpv(
                        Compiler,
                        SourcePtr,
                        (nuint)SourceBytes.Length,
                        Kind,
                        Name,
                        "main",
                        Options);

                    try
                    {
                        if (ShadercApi.ResultGetCompilationStatus(Result) != CompilationStatus.Success)
                        {
                            byte* Message = ShadercApi.ResultGetErrorMessage(Result);
                            Error = Marshal.PtrToStringUTF8((nint)Message) ?? $"Shader compilation failed for {Name}.";
                            return Array.Empty<byte>();
                        }

                        nuint ByteLength = ShadercApi.ResultGetLength(Result);
                        byte* Bytes = ShadercApi.ResultGetBytes(Result);
                        byte[] Spirv = new byte[(int)ByteLength];
                        Marshal.Copy((nint)Bytes, Spirv, 0, Spirv.Length);
                        return Spirv;
                    }
                    finally
                    {
                        ShadercApi.ResultRelease(Result);
                    }
                }
            }
            finally
            {
                ShadercApi.CompileOptionsRelease(Options);
                ShadercApi.CompilerRelease(Compiler);
                ShadercApi.Dispose();
            }
        }
    }
}
