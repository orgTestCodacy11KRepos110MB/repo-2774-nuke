// Copyright 2021 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FluentAssertions;
using Nuke.Common.IO;
using Nuke.Common.Utilities.Collections;
using Nuke.Common.ValueInjection;
using Xunit;
using static Nuke.Common.Utilities.ReflectionUtility;

namespace Nuke.Common.Tests
{
    public class ParameterServiceTest
    {
        private ParameterService GetService(string[] commandLineArguments = null, IDictionary<string, string> environmentVariables = null)
        {
            commandLineArguments ??= new string[0];
            environmentVariables ??= new Dictionary<string, string>();

            return new ParameterService(
                () => commandLineArguments,
                () => environmentVariables.AsReadOnly());
        }

        [Theory]
        [InlineData("configuration", typeof(string), "release")]
        [InlineData("AMOUNT", typeof(int), 5)]
        [InlineData("noLogo", typeof(bool), true)]
        [InlineData("NoDeps", typeof(bool), false)]
        [InlineData("root", typeof(AbsolutePath), null)]
        public void TestConversion(string argument, Type destinationType, object expectedValue)
        {
            GetService(
                new[]
                {
                    "-nologo",
                    "-configuration",
                    "release",
                    "-files",
                    "C:\\new folder\\file.txt",
                    "C:\\file.txt",
                    "-amount",
                    "5",
                    "--no-deps",
                    "false",
                    "--root"
                }).GetCommandLineArgument(argument, destinationType, separator: null).Should().Be(expectedValue);
        }

        [Theory]
        [InlineData("MSBuildConfiguration", typeof(string), "msbcfg")]
        [InlineData("dockerConfiguration", typeof(string), "dkrcfg")]
        [InlineData("publish-dir", typeof(string), "dir")]
        public void TestSplitted(string argument, Type destinationType, object expectedValue)
        {
            GetService(
                new[]
                {
                    "-msbuild-configuration",
                    "msbcfg",
                    "-docker-configuration",
                    "dkrcfg",
                    "--publish-dir",
                    "dir"
                }).GetCommandLineArgument(argument, destinationType, separator: null).Should().Be(expectedValue);
        }

        [Theory]
        [InlineData(typeof(bool), false)]
        [InlineData(typeof(bool?), null)]
        [InlineData(typeof(int), 0)]
        [InlineData(typeof(int?), null)]
        [InlineData(typeof(string), null)]
        [InlineData(typeof(string[]), null)]
        [InlineData(typeof(AbsolutePath), null)]
        public void TestNotSupplied(Type destinationType, object expectedValue)
        {
            GetService().GetCommandLineArgument("notsupplied", destinationType, separator: null).Should().Be(expectedValue);
        }

        [Theory]
        [InlineData("arg1", typeof(string), "value1")]
        [InlineData("arg2", typeof(string), "value3")]
        [InlineData("switch2", typeof(bool), true)]
        [InlineData("switch3", typeof(bool), false)]
        [InlineData("notsupplied1", typeof(bool), false)]
        [InlineData("notsupplied2", typeof(int?), null)]
        public void TestEnvironmentVariables(string parameter, Type destinationType, object expectedValue)
        {
            var service = GetService(
                new[]
                {
                    "-arg1",
                    "value1",
                    "-switch1"
                },
                new Dictionary<string, string>
                {
                    { "arg1", "value2" },
                    { "arg2", "value3" },
                    { "switch2", "true" },
                    { "switch3", "false" }
                });
            service.GetParameter(parameter, destinationType, separator: null).Should().Be(expectedValue);
        }

        [Fact]
        public void TestConversionSpecial()
        {
            var dateTime = DateTime.Now;
            var guid = Guid.NewGuid();
            var service = GetService(
                new[]
                {
                    "-guid",
                    $"{guid}",
                    "-datetime",
                    $"{dateTime.ToString(CultureInfo.InvariantCulture)}",
                    "--root",
                    "/bin/etc"
                });

            service.GetParameter("datetime", destinationType: typeof(DateTime), separator: null)
                .Should().BeOfType<DateTime>().Subject.Should().BeCloseTo(dateTime, TimeSpan.FromSeconds(1));

            service.GetParameter("guid", destinationType: typeof(Guid), separator: null)
                .Should().BeOfType<Guid>().Subject.Should().Be(guid);

            service.GetParameter("root", destinationType: typeof(AbsolutePath), separator: null)
                .Should().BeOfType<AbsolutePath>().Subject.ToString().Should().Be("/bin/etc");
        }

        [Fact]
        public void TestConversionCollections()
        {
            var service = GetService(
                new[]
                {
                    "-files",
                    "C:\\new folder\\file.txt",
                    "C:\\file.txt",
                    "-amount",
                    "5"
                },
                new Dictionary<string, string>
                {
                    { "values", "A+B+C" }
                });

            service.GetParameter("files", destinationType: typeof(string[]), separator: null)
                .Should().BeOfType<string[]>().Subject.Should().BeEquivalentTo("C:\\new folder\\file.txt", "C:\\file.txt");

            service.GetParameter("values", destinationType: typeof(string[]), separator: '+')
                .Should().BeOfType<string[]>().Subject.Should().BeEquivalentTo("A", "B", "C");
        }

        [Theory]
        [InlineData(new[] { "arg1" }, 0, typeof(string), "arg1")]
        [InlineData(new[] { "true" }, 0, typeof(bool), true)]
        [InlineData(new[] { "arg1" }, 1, typeof(string), null)]
        [InlineData(new[] { "arg1", "arg2" }, 1, typeof(string), "arg2")]
        [InlineData(new[] { "posArg", "-NamedArg", "value" }, 0, typeof(string), "posArg")]
        [InlineData(new[] { "posArg", "-NamedArg", "value" }, 1, typeof(string), null)]
        [InlineData(new[] { "posArg", "-NamedArg", "value" }, 2, typeof(string), null)]
        [InlineData(new[] { "arg1", "arg2", "arg3" }, -1, typeof(string), "arg3")]
        [InlineData(new[] { "arg1", "arg2", "arg3" }, -2, typeof(string), "arg2")]
        public void TestPositionalCommandLineArguments(string[] commandLineArgs, int position, Type destinationType, object expectedValue)
        {
            var service = GetService(commandLineArgs);
            service.GetCommandLineArgument(position, destinationType, separator: null).Should().Be(expectedValue);
        }

        [Fact]
        public void TestExpression()
        {
            var service = GetService(
                new[]
                {
                    "--string",
                    "--set",
                    "1",
                    "2",
                    "3",
                    "--interface-param"
                });

            var build = new TestBuild();

            ParameterService.GetFromMemberInfo(GetMemberInfo(() => build.String), typeof(AbsolutePath), service.GetParameter)
                .Should().BeNull();

            ParameterService.GetFromMemberInfo(GetMemberInfo(() => build.String), typeof(bool), service.GetParameter)
                .Should().BeOfType<bool>().Subject.Should().BeTrue();

            ParameterService.GetFromMemberInfo(GetMemberInfo(() => build.Set), destinationType: null, service.GetParameter)
                .Should().BeOfType<int[]>().Subject.Should().BeEquivalentTo(new[] { 1, 2, 3 });

            ParameterService.GetFromMemberInfo(GetMemberInfo(() => ((ITestComponent)build).Param), destinationType: null, service.GetParameter)
                .Should().BeOfType<bool>().Subject.Should().BeTrue();
        }

        [Fact]
        public void TestValueSet()
        {
            var build = new TestBuild();
            var verbosities = new[]
                              {
                                  (nameof(Verbosity.Minimal), Verbosity.Minimal),
                                  (nameof(Verbosity.Normal), Verbosity.Normal),
                                  (nameof(Verbosity.Quiet), Verbosity.Quiet),
                                  (nameof(Verbosity.Verbose), Verbosity.Verbose),
                              };
            ParameterService.GetParameterValueSet(GetMemberInfo(() => NukeBuild.Verbosity), instance: null)
                .Should().BeEquivalentTo(verbosities);
            ParameterService.GetParameterValueSet(GetMemberInfo(() => build.Verbosities), instance: null)
                .Should().BeEquivalentTo(verbosities);
        }

#pragma warning disable CS0649
        private class TestBuild : NukeBuild, ITestComponent
        {
            [Parameter] public string String;
            [Parameter] public int[] Set;
            [Parameter] public Verbosity[] Verbosities;
        }

        [ParameterPrefix("Interface")]
        private interface ITestComponent : INukeBuild
        {
            [Parameter] bool Param => TryGetValue<bool?>(() => Param) ?? false;
        }
#pragma warning restore CS0649
    }
}
