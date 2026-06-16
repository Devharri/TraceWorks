using System.Threading.Channels;
using TraceWorks.Protocols.S7.Services;
using TraceWorks.Shared.Services;
using TraceWorks.Shared.Models;
using TraceWorks.Storage.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Application services
builder.Services.AddSingleton(Channel.CreateBounded<SampleModel>(10000));
builder.Services.AddSingleton<TagConfigurationService>();
builder.Services.AddSingleton<PlcConfigurationService>();
builder.Services.AddSingleton<MetricsService>();

//Hosted services
builder.Services.AddHostedService<S7AcquisitionService>();
builder.Services.AddHostedService<StorageService>();
builder.Services.AddHostedService<MetricsReporterService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();