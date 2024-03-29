﻿// Copyright 2021 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.ComponentModel;
using System.Linq;
using JetBrains.Annotations;
using Nuke.Common.Tooling;

namespace Nuke.Components
{
    [PublicAPI]
    [TypeConverter(typeof(TypeConverter<Configuration>))]
    public class Configuration : Enumeration
    {
        public static Configuration Debug = new Configuration { Value = nameof(Debug) };
        public static Configuration Release = new Configuration { Value = nameof(Release) };

        public static implicit operator string(Configuration configuration)
        {
            return configuration.Value;
        }
    }
}
