
namespace BaggageServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            //app.UseHttpsRedirection();
            app.UseDefaultFiles(); // ��������� ����������� index.html
            app.UseStaticFiles(); // ��������� ����������� ����������� �����                                   
            app.UseCors(builder => // ��������� CORS
                builder.WithOrigins("http://127.0.0.1:5500") // ����� ������� ����������� ���������
                       .AllowAnyHeader()
                       .AllowAnyMethod());

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
