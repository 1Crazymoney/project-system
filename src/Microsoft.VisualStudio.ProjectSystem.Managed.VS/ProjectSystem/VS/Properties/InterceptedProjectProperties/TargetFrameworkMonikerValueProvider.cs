﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;
using NuGet.VisualStudio;
using System;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Properties
{
    [ExportInterceptingPropertyValueProvider("TargetFrameworkMoniker", ExportInterceptingPropertyValueProviderFile.ProjectFile)]
    internal sealed class TargetFrameworkMonikerValueProvider : InterceptingPropertyValueProviderBase
    {
        private IUnconfiguredProjectVsServices _unconfiguredProjectVsServices;
        private readonly ProjectProperties _properties;
        private readonly IVsFrameworkParser _frameworkParser;
        private const string _targetFrameworkProperty = "TargetFramework";
        private const string _targetFrameworksProperty = "TargetFrameworks";

        [ImportingConstructor]
        public TargetFrameworkMonikerValueProvider(IUnconfiguredProjectVsServices unconfiguredProjectVsServices, ProjectProperties properties, IVsFrameworkParser frameworkParser)
        {
            _unconfiguredProjectVsServices = unconfiguredProjectVsServices;
            _properties = properties;
            _frameworkParser = frameworkParser;
        }

        public override async Task<string> OnSetPropertyValueAsync(string unevaluatedPropertyValue, IProjectProperties defaultProperties, IReadOnlyDictionary<string, string> dimensionalConditions = null)
        {
            var configuration = await _properties.GetConfigurationGeneralPropertiesAsync().ConfigureAwait(false);
            var currentTargetFrameWork = (string)await configuration.TargetFramework.GetValueAsync().ConfigureAwait(true);
            var currentTargetFrameWorks = (string)await configuration.TargetFrameworks.GetValueAsync().ConfigureAwait(true);
            if (!string.IsNullOrEmpty(currentTargetFrameWorks))
            {
                throw new Exception(VSResources.MultiTFEditNotSupported);
            }
            else if (!string.IsNullOrEmpty(currentTargetFrameWork))
            {
                var frameworkName = new FrameworkName(unevaluatedPropertyValue);
                await defaultProperties.SetPropertyValueAsync(_targetFrameworkProperty, _frameworkParser.GetShortFrameworkName(frameworkName)).ConfigureAwait(true);
            }
            else
            {
                // CPS implements IVsHierarchy.SetProperty for the TFM property to call through the multi-targeting service and change the TFM.
                // This causes the project to be reloaded after changing the values.
                // Since the property providers are called under a write-lock, trying to reload the project on the same context fails saying it can't load the project
                // if a lock is held. We are not going to write to the file under this lock (we return null from this method) and so we fork execution here to schedule
                // a lambda on the UI thread and we don't pass the lock information from this context to the new one. 
                _unconfiguredProjectVsServices.ThreadingService.Fork(() =>
                {
                    _unconfiguredProjectVsServices.VsHierarchy.SetProperty(HierarchyId.Root, (int)VsHierarchyPropID.TargetFrameworkMoniker, unevaluatedPropertyValue);
                    return System.Threading.Tasks.Task.CompletedTask;
                }, options: ForkOptions.HideLocks | ForkOptions.StartOnMainThread);
            }
            return await System.Threading.Tasks.Task.FromResult<string>(null).ConfigureAwait(false);
        }
    }
}
