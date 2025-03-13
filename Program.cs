
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
            app.UseDefaultFiles(); // Позволяет обслуживать index.html
            app.UseStaticFiles(); // Позволяет обслуживать статические файлы                                   
            app.UseCors(builder => // Добавляем CORS
                builder.WithOrigins("https://baggage.reaport.ru/") 
                       .AllowAnyHeader()
                       .AllowAnyMethod());

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
