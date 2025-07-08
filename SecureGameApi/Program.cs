using AspNetCoreRateLimit;

// Ensure the NuGet package for AspNetCoreRateLimit is installed.  
// Run the following command in the Package Manager Console or add it via the NuGet Package Manager in Visual Studio:  
// Install-Package AspNetCoreRateLimit

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowGameClient", policy =>
    {
        policy
          .WithOrigins("")    //TODO: oyunun çalýþtýðý adresi gir
          .AllowAnyMethod()
          .WithHeaders("Content-Type", "X-Signature");
    });
});
builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddPolicy("Default", p =>
{
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
}));


// 1) appsettings.json’den IpRateLimiting bölümü
builder.Services.AddOptions();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));

// 2) Rate limit store & counter
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();


// 3) Rate limiter
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

app.UseCors("Default");
// Rate limiting middleware
app.UseIpRateLimiting();

// ... routing, endpoints vs.
app.MapControllers();

app.UseCors("AllowGameClient");


app.MapControllers();
app.Run();

