﻿// Copyright 2021 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using Nuke.Common.Execution;

namespace Nuke.Common.ValueInjection
{
    public class InjectNonParameterValuesAttribute : BuildExtensionAttributeBase, IOnBuildInitialized
    {
        public void OnBuildInitialized(
            NukeBuild build,
            IReadOnlyCollection<ExecutableTarget> executableTargets,
            IReadOnlyCollection<ExecutableTarget> executionPlan)
        {
            ValueInjectionUtility.InjectValues(build, x => x is not ParameterAttribute);
        }
    }
}
