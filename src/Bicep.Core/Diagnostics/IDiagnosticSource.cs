// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Generic;

namespace Bicep.Core.Diagnostics
{
    public interface IDiagnosticSource
    {
        IEnumerable<IDiagnostic> GetDiagnostics();
    }
}