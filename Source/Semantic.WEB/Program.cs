using Semantic.WEB.ApplicationLayer;

namespace Semantic.WEB
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddMemoryCache();

            builder.Services.AddSingleton<OpenDataService>();

            // Register IHttpClientFactory
            builder.Services.AddHttpClient("wikidata", client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("SemanticTourismApp/1.0");
            });

            builder.Services.AddRazorPages();

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }

            // get OpenDataService and call warmup

            var services = app.Services;
            var openDataService = services.GetService<OpenDataService>();
            openDataService.WarmUp();

            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();

            app.MapControllers();   // Required for your API endpoints
            app.MapRazorPages();

            app.Run();
        }
    }
}
