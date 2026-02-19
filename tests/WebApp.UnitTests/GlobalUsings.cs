global using System;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Options;
global using eShop.WebApp;
global using eShop.WebApp.Services.Payments;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using NSubstitute;

[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

