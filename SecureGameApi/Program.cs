var builder = WebApplication.CreateBuilder(args);

// API Controller'lar� ekle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Swagger a��k olsun
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Statik dosyalar� sun
app.UseDefaultFiles(); // index.html'yi otomatik y�klemek i�in
app.UseStaticFiles();  // wwwroot klas�r�nden servis

app.UseAuthorization();

app.MapControllers();

app.Run();
