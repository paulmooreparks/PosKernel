//
// Copyright 2025 Paul Moore Parks and contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using PosKernel.HttpService.Models;
using PosKernel.HttpService.Services;
using System.Collections.Concurrent;
using Serilog;
using PosKernel.Configuration;

// Configure Serilog logging to file
var logDirectory = Path.Combine(PosKernelConfiguration.ConfigDirectory, "logs");
Directory.CreateDirectory(logDirectory);

var logFilePath = Path.Combine(logDirectory, "http-service.log");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File(logFilePath,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Use Serilog for ASP.NET Core
builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add our in-memory storage services
builder.Services.AddSingleton<ISessionStorage, InMemorySessionStorage>();
builder.Services.AddSingleton<ITransactionStorage>(serviceProvider =>
{
    var sessionStorage = serviceProvider.GetRequiredService<ISessionStorage>();
    var logger = serviceProvider.GetRequiredService<ILogger<InMemoryTransactionStorage>>();
    return new InMemoryTransactionStorage(sessionStorage, logger);
});
builder.Services.AddSingleton<ILineItemStorage, InMemoryLineItemStorage>();

var app = builder.Build();

// Add startup banner like restaurant extension
Log.Information("🌐 POS Kernel HTTP Service v1.0.0");
Log.Information("Culture-neutral transaction kernel with HTTP API");

// Report log file location for CLI orchestrator
Console.WriteLine($"POSKERNEL_LOG_FILE: {logFilePath}");
Log.Information("POSKERNEL_LOG_FILE: {LogFile}", logFilePath);

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

// Health check endpoint with log file location
app.MapGet("/health", () => new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    logFile = logFilePath,
    version = "1.0.0"
});

Log.Information("POS Kernel HTTP Service v{Version} listening on {Address}",
    "1.0.0", "http://localhost:8080");

// Test direct logging to file
Log.Information("SERILOG: HTTP Service startup complete with file logging");

app.Run("http://localhost:8080");
