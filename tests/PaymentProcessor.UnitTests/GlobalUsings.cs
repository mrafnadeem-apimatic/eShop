global using System;
global using System.Threading;
global using System.Threading.Tasks;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using NSubstitute;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using eShop.EventBus.Abstractions;
global using eShop.PaymentProcessor.IntegrationEvents.Events;
global using eShop.PaymentProcessor.IntegrationEvents.EventHandling;
global using eShop.PaymentProcessor.Services;

[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]
