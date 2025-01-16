using Amazon.S3;
using FileServer_POC.Entities;
using FileServer_POC.Repositories;
using FileServer_POC.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using System;
using Amazon.Extensions.NETCore.Setup;
using Amazon.S3;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IFileRepository, FileRepository>();
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
// Register Amazon S3 Client
builder.Services.AddAWSService<IAmazonS3>(builder.Configuration.GetAWSOptions()); ;


builder.Services.Configure<FormOptions>(options => {
    options.MultipartBodyLengthLimit = 2147483648; // 2GB
});

//builder.Services.AddDbContext<ApplicationDbContext>(options =>
//    options.UseSqlite("Data Source=fileServer.db"));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

var app = builder.Build();

// Configure the HTTP request pipeline.

using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
        
    }
}

//**NOTE: UNCOMMENT FOR RUNNING WITHOUT DOCKER
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.IsEnvironment("Docker"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FileServer API v1");
        c.RoutePrefix = string.Empty; 
    });
}


app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
