var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddDotnetProject("apiservice", "../SmartLogistics.ApiService/SmartLogistics.ApiService.csproj")
    .WithHttpHealthCheck("/health");

builder.AddDotnetProject("webfrontend", "../SmartLogistics.Web/SmartLogistics.Web.csproj")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
