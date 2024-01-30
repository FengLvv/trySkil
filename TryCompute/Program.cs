using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;
using Silk.NET.Maths;
using Veldrid.OpenGLBinding;
using ClearBufferMask = Silk.NET.OpenGL.ClearBufferMask;
using DrawElementsType = Silk.NET.OpenGL.DrawElementsType;
using GetPName = Silk.NET.OpenGL.GetPName;
using PrimitiveType = Silk.NET.OpenGL.PrimitiveType;
using ShaderType = Silk.NET.OpenGL.ShaderType;
using TextureParameterName = Silk.NET.OpenGL.TextureParameterName;
using TextureTarget = Silk.NET.OpenGL.TextureTarget;
using TextureUnit = Silk.NET.OpenGL.TextureUnit;
using VertexAttribPointerType = Silk.NET.OpenGL.VertexAttribPointerType;

namespace Tutorial {
	class Program {
		private static IWindow window;
		private static GL Gl;

		private static uint Vbo;
		private static uint Ebo;
		private static uint Vao;
		private static uint Shader;

		static uint tex_w;
		static uint tex_h;
		static uint ray_program;

		//Vertex shaders are run on each vertex.
		private static readonly string VertexShaderSource = @"
        #version 430 core //Using version GLSL version 3.3
        layout (location = 0) in vec4 vPos;
        
        void main()
        {
            gl_Position = vec4(vPos.x, vPos.y, vPos.z, 1.0);
        }
        ";

		//Fragment shaders are run on each fragment/pixel of the geometry.
		private static readonly string FragmentShaderSource = @"
        #version 430 core
        out vec4 FragColor;
		layout(binding = 0) uniform sampler2D colortexture;

		in vec2 TexCoord;

        void main()
        {
			vec4 texColor = texture(colortexture, TexCoord);
            FragColor = texColor;
        }
        ";

		private static readonly string the_ray_shader_string = @"
		#version 430 core
		layout(local_size_x = 1, local_size_y = 1) in;
		layout(rgba32f, binding = 0) uniform image2D img_output;

		void main() {
		  // base pixel colour for image
		  vec4 pixel = vec4(0.0, 0.0, 1.0, 1.0);
		  // get index in global work group i.e x,y position
		  ivec2 pixel_coords = ivec2(gl_GlobalInvocationID.xy);
		  
		  //
		  // interesting stuff happens here later
		  //
		  
		  // output to a specific pixel in the image
		  imageStore(img_output, pixel_coords, pixel);
		}
		";

		//Vertex data, uploaded to the VBO.
		private static readonly float[] Vertices ={
			//X    Y      Z
			0.5f,
			0.5f,
			0.0f,
			0.5f,
			-0.5f,
			0.0f,
			-0.5f,
			-0.5f,
			0.0f,
			-0.5f,
			0.5f,
			0.5f
		};

		//Index data, uploaded to the EBO.
		private static readonly uint[] Indices ={
			0,
			1,
			3,
			1,
			2,
			3
		};
		static uint tex_output;



		private static void Main( string[] args ) {
			var options = WindowOptions.Default;
			options.Size = new Vector2D<int>( 800, 600 );
			options.Title = "LearnOpenGL with Silk.NET";
			window = Window.Create( options );

			window.Load += OnLoad;
			window.Render += OnRender;
			window.Update += OnUpdate;
			window.Closing += OnClose;

			window.Run();

			window.Dispose();
		}


		private static unsafe void OnLoad() {
			IInputContext input = window.CreateInput();
			for( int i = 0; i < input.Keyboards.Count; i++ ) {
				input.Keyboards[i].KeyDown += KeyDown;
			}

			//Getting the opengl api for drawing to the screen.
			Gl = GL.GetApi( window );

			//Creating a vertex array.
			Vao = Gl.GenVertexArray();
			Gl.BindVertexArray( Vao );

			//Initializing a vertex buffer that holds the vertex data.
			Vbo = Gl.GenBuffer(); //Creating the buffer.
			Gl.BindBuffer( BufferTargetARB.ArrayBuffer, Vbo ); //Binding the buffer.
			fixed (void* v = &Vertices[0]) {
				Gl.BufferData( BufferTargetARB.ArrayBuffer, (nuint)( Vertices.Length * sizeof( uint ) ), v, BufferUsageARB.StaticDraw ); //Setting buffer data.
			}

			//Initializing a element buffer that holds the index data.
			Ebo = Gl.GenBuffer(); //Creating the buffer.
			Gl.BindBuffer( BufferTargetARB.ElementArrayBuffer, Ebo ); //Binding the buffer.
			fixed (void* i = &Indices[0]) {
				Gl.BufferData( BufferTargetARB.ElementArrayBuffer, (nuint)( Indices.Length * sizeof( uint ) ), i, BufferUsageARB.StaticDraw ); //Setting buffer data.
			}

			//Creating a vertex shader.
			uint vertexShader = Gl.CreateShader( ShaderType.VertexShader );
			Gl.ShaderSource( vertexShader, VertexShaderSource );
			Gl.CompileShader( vertexShader );

			//Checking the shader for compilation errors.
			string infoLog = Gl.GetShaderInfoLog( vertexShader );
			if( !string.IsNullOrWhiteSpace( infoLog ) ) {
				Console.WriteLine( $"Error compiling vertex shader {infoLog}" );
			}

			//Creating a fragment shader.
			uint fragmentShader = Gl.CreateShader( ShaderType.FragmentShader );
			Gl.ShaderSource( fragmentShader, FragmentShaderSource );
			Gl.CompileShader( fragmentShader );

			//Checking the shader for compilation errors.
			infoLog = Gl.GetShaderInfoLog( fragmentShader );
			if( !string.IsNullOrWhiteSpace( infoLog ) ) {
				Console.WriteLine( $"Error compiling fragment shader {infoLog}" );
			}

			//Combining the shaders under one shader program.
			Shader = Gl.CreateProgram();
			Gl.AttachShader( Shader, vertexShader );
			Gl.AttachShader( Shader, fragmentShader );
			Gl.LinkProgram( Shader );

			//Checking the linking for errors.
			Gl.GetProgram( Shader, GLEnum.LinkStatus, out var status );
			if( status == 0 ) {
				Console.WriteLine( $"Error linking shader {Gl.GetProgramInfoLog( Shader )}" );
			}

			//Delete the no longer useful individual shaders;
			Gl.DetachShader( Shader, vertexShader );
			Gl.DetachShader( Shader, fragmentShader );
			Gl.DeleteShader( vertexShader );
			Gl.DeleteShader( fragmentShader );

			//Tell opengl how to give the data to the shaders.
			Gl.VertexAttribPointer( 0, 3, VertexAttribPointerType.Float, false, 3 * sizeof( float ), null );
			Gl.EnableVertexAttribArray( 0 );

			//bind compute shader
//设置compute shader
			tex_w = 512;
			tex_h = 512;
			Gl.GenTextures( 1, out tex_output );
			Gl.BindTexture( TextureTarget.Texture2D, tex_output );
			Gl.TexParameterI( TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge );
			Gl.TexParameterI( TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge );
			Gl.TexParameterI( TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear );
			Gl.TexParameterI( TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear );

			Gl.TexImage2D( TextureTarget.Texture2D, 0, InternalFormat.Rgba32f, (uint)tex_w, (uint)tex_h, 0, PixelFormat.Rgba, PixelType.Float, null );
			Gl.BindImageTexture( 0, tex_output, 0, false, 0, GLEnum.WriteOnly, GLEnum.Rgba32f );
//获取compute shader的最大工作组数量
			Gl.GetInteger( GetPName.MaxComputeWorkGroupSize, 0, out int work_group_size1 );
			Gl.GetInteger( GetPName.MaxComputeWorkGroupSize, 1, out int work_group_size2 );
			Gl.GetInteger( GetPName.MaxComputeWorkGroupSize, 2, out int work_group_size3 );
			Console.WriteLine( $"MaxComputeWorkGroupSize: {work_group_size1} {work_group_size2} {work_group_size3}" );
			Gl.GetInteger( GetPName.MaxComputeWorkGroupInvocations, out int work_group_invocations );
			Console.WriteLine( $"MaxComputeWorkGroupInvocations: {work_group_invocations}" );

			uint ray_shader = Gl.CreateShader( GLEnum.ComputeShader );
			Gl.ShaderSource( ray_shader, the_ray_shader_string );

//Combining the shaders under one shader program.
			ray_program = Gl.CreateProgram();
			Gl.AttachShader( ray_program, ray_shader );
			Gl.LinkProgram( ray_program );
		}

		private static unsafe void OnRender( double obj ) //Method needs to be unsafe due to draw elements.
		{
			//execute compute shader
			Gl.UseProgram( ray_program );
			Gl.DispatchCompute( tex_w, tex_h, 1 );
			//确保compute shader执行完毕
			Gl.MemoryBarrier( MemoryBarrierMask.ShaderImageAccessBarrierBit );

			//Clear the color channel.
			Gl.Clear( (uint)ClearBufferMask.ColorBufferBit );

			//Bind the geometry and shader.
			Gl.UseProgram( Shader );
			Gl.BindVertexArray( Vao );
			Gl.ActiveTexture( TextureUnit.Texture0 );
			Gl.BindTexture( TextureTarget.Texture2D, tex_output );

			//Draw the geometry.
			Gl.DrawElements( PrimitiveType.Triangles, (uint)Indices.Length, DrawElementsType.UnsignedInt, null );
		}

		private static void OnUpdate( double obj ) {

		}

		private static void OnClose() {
			//Remember to delete the buffers.
			Gl.DeleteBuffer( Vbo );
			Gl.DeleteBuffer( Ebo );
			Gl.DeleteVertexArray( Vao );
			Gl.DeleteProgram( Shader );
		}

		private static void KeyDown( IKeyboard arg1, Key arg2, int arg3 ) {
			if( arg2 == Key.Escape ) {
				window.Close();
			}
		}
	}
}