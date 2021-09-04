// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Bicep.Core.Diagnostics
{
    public class ConcurrentDiagnosticWriter : IDiagnosticWriter, IDiagnosticSource
    {
        private readonly ConcurrentQueue<IDiagnostic> diagnostics;

        public ConcurrentDiagnosticWriter()
        {
            this.diagnostics = new();
        }

        public void Write(IDiagnostic diagnostic)
            => diagnostics.Enqueue(diagnostic);

        public IEnumerable<IDiagnostic> GetDiagnostics() => diagnostics.ToImmutableArray();
    }
}