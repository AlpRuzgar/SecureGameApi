var builder = WebApplication.CreateBuilder(args);

// API Controller'larý ekle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Swagger açýk olsun
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Statik dosyalarý sun
app.UseDefaultFiles(); // index.html'yi otomatik yüklemek için
app.UseStaticFiles();  // wwwroot klasöründen servis

app.UseAuthorization();

app.MapControllers();

app.Run();
