using System.Runtime.InteropServices;

using Silk.NET.Shaderc;

namespace AgGradeDesigner.Core.Rendering.Vulkan
{
    /// <summary>
    /// Compiles embedded GLSL sources to SPIR-V via Shaderc.
    /// </summary>
    static internal unsafe class VulkanShaderCompiler
    {
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
            } cam;

            layout(location = 0) out vec4 fragColor;

            void main() {
                float Ex = inPosition.x;
                float Ey = inPosition.y;
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

        private const string FieldFragmentShaderGlsl = """
            #version 450

            layout(location = 0) in vec4 fragColor;
            layout(location = 0) out vec4 outColor;

            void main() {
                outColor = fragColor;
            }
            """;

        static public byte[] CompileVertexShader
            (
            out string? Error
            )
        {
            return Compile(FieldVertexShaderGlsl, ShaderKind.VertexShader, "field.vert", out Error);
        }

        static public byte[] CompileFragmentShader
            (
            out string? Error
            )
        {
            return Compile(FieldFragmentShaderGlsl, ShaderKind.FragmentShader, "field.frag", out Error);
        }

        static public byte[] CompileTexturedVertexShader
            (
            out string? Error
            )
        {
            return Compile(TexturedVertexShaderGlsl, ShaderKind.VertexShader, "field_tex.vert", out Error);
        }

        static public byte[] CompileTexturedFragmentShader
            (
            out string? Error
            )
        {
            return Compile(TexturedFragmentShaderGlsl, ShaderKind.FragmentShader, "field_tex.frag", out Error);
        }

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
            } cam;

            layout(location = 0) out vec2 fragTexCoord;

            void main() {
                float Ex = inPosition.x;
                float Ey = inPosition.y;
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

        private const string TexturedFragmentShaderGlsl = """
            #version 450

            layout(location = 0) in vec2 fragTexCoord;
            layout(set = 0, binding = 1) uniform sampler2D tileSampler;
            layout(location = 0) out vec4 outColor;

            void main() {
                outColor = texture(tileSampler, fragTexCoord);
            }
            """;

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
