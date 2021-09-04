// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Concurrent;
using System.Collections.Generic;
using Bicep.Core.Syntax;
using Bicep.Core.Diagnostics;

namespace Bicep.Core.Semantics.Metadata
{
    public abstract class SyntaxMetadataCacheBase<TMetadata> : IDiagnosticSource
    {
        private readonly ConcurrentDictionary<SyntaxBase, TMetadata> cache;
        private readonly ConcurrentDiagnosticWriter diagnosticWriter;

        protected SyntaxMetadataCacheBase()
        {
            cache = new();
            diagnosticWriter = new();
        }
        
        protected abstract TMetadata Calculate(SyntaxBase syntax, IDiagnosticWriter diagnosticWriter);

        public TMetadata? TryLookup(SyntaxBase syntax)
        {
            return cache.GetOrAdd(
                syntax,
                syntax => Calculate(syntax, diagnosticWriter));
        }

        public IEnumerable<IDiagnostic> GetDiagnostics()
            => diagnosticWriter.GetDiagnostics();
    }
}
