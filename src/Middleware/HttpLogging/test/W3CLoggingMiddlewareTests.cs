// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.HttpLogging
{
    public class W3CLoggingMiddlewareTests
    {
        [Fact]
        public void Ctor_ThrowsExceptionsWhenNullArgs()
        {
            var options = CreateOptionsAccessor();
            Assert.Throws<ArgumentNullException>(() => new W3CLoggingMiddleware(
                null,
                options,
                new TestW3CLogger(options, new HostingEnvironment(), NullLoggerFactory.Instance)));

            Assert.Throws<ArgumentNullException>(() => new W3CLoggingMiddleware(c =>
                {
                    return Task.CompletedTask;
                },
                null,
                new TestW3CLogger(options, new HostingEnvironment(), NullLoggerFactory.Instance)));


            Assert.Throws<ArgumentNullException>(() => new W3CLoggingMiddleware(c =>
                {
                    return Task.CompletedTask;
                },
                options,
                null));
        }

        [Fact]
        public async Task NoopWhenLoggingDisabled()
        {
            var options = CreateOptionsAccessor();
            options.CurrentValue.LoggingFields = W3CLoggingFields.None;

            var middleware = new TestW3CLoggingMiddleware(
                c =>
                {
                    c.Response.StatusCode = 200;
                    return Task.CompletedTask;
                },
                options,
                new TestW3CLogger(options, new HostingEnvironment(), NullLoggerFactory.Instance));

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Protocol = "HTTP/1.0";
            httpContext.Request.Method = "GET";
            httpContext.Request.Path = new PathString("/foo");
            httpContext.Request.QueryString = new QueryString("?foo");
            httpContext.Request.Headers["Referer"] = "bar";

            await middleware.Invoke(httpContext);

            Assert.Empty(middleware.Logger.Processor.Lines);
        }

        [Fact]
        public async Task DefaultDoesNotLogOptionalFields()
        {
            var options = CreateOptionsAccessor();

            var middleware = new TestW3CLoggingMiddleware(
                c =>
                {
                    c.Response.StatusCode = 200;
                    return Task.CompletedTask;
                },
                options,
                new TestW3CLogger(options, new HostingEnvironment(), NullLoggerFactory.Instance));

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Protocol = "HTTP/1.0";
            httpContext.Request.Headers["Cookie"] = "Snickerdoodle";
            httpContext.Response.StatusCode = 200;

            var now = DateTime.Now;
            await middleware.Invoke(httpContext);
            await middleware.Logger.WaitForWrites(4).DefaultTimeout();

            var lines = middleware.Logger.Processor.Lines;
            Assert.Equal("#Version: 1.0", lines[0]);

            Assert.StartsWith("#Start-Date: ", lines[1]);
            var startDate = DateTime.Parse(lines[1].Substring(13), CultureInfo.InvariantCulture);
            // Assert that the log was written in the last 10 seconds
            Assert.True(now.Subtract(startDate).TotalSeconds < 10);

            Assert.Equal("#Fields: date time c-ip s-computername s-ip s-port cs-method cs-uri-stem cs-uri-query sc-status time-taken cs-version cs-host cs(User-Agent) cs(Referer)", lines[2]);
            Assert.DoesNotContain(lines[2], "Snickerdoodle");
        }

        private IOptionsMonitor<W3CLoggerOptions> CreateOptionsAccessor()
        {
            var options = new W3CLoggerOptions();
            var optionsAccessor = Mock.Of<IOptionsMonitor<W3CLoggerOptions>>(o => o.CurrentValue == options);
            return optionsAccessor;
        }
    }
}
