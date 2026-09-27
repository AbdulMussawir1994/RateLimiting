using RateLimiting.ExternalHelpers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEnterpriseRateLimiting();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Rate Limiting",
        Version = "v1",
        Description = "Let's Build Full Stack",
        Contact = new Microsoft.OpenApi.OpenApiContact
        {
            Name = "API"
        }
    });
});

var app = builder.Build();


app.UseSwagger();
app.UseSwaggerUI(opt =>
{
    opt.SwaggerEndpoint("/swagger/v1/swagger.json", "Rate Limitter");
    opt.RoutePrefix = "swagger";
    opt.DocumentTitle = "API";
});

app.UseRateLimiter();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
