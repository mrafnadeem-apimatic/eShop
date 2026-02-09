global using System;
global using System.Net;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;
global using eShop.EventBus.Abstractions;
global using eShop.EventBus.Events;
global using eShop.PaymentProcessor;
global using eShop.PaymentProcessor.IntegrationEvents.EventHandling;
global using eShop.PaymentProcessor.IntegrationEvents.Events;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using NSubstitute;

[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

