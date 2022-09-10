using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;

namespace CodeLensOopProvider
{
    [Export(typeof(IAsyncCodeLensDataPointProvider))]
    [Name(Id)]
    [ContentType("code")]
    //[Priority(200)]
    internal class GitCommitDataPointProvider : IAsyncCodeLensDataPointProvider
    {
        internal const string Id = "DisasmoLensProvider";

        public Task<bool> CanCreateDataPointAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext context, CancellationToken token)
        {
            if (descriptor == null || context == null)
                return Task.FromResult(false);

            switch (descriptor.Kind)
            {
                case CodeElementKinds.Class:
                case CodeElementKinds.Struct:
                case CodeElementKinds.Member:
                case CodeElementKinds.Method:
                    return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<IAsyncCodeLensDataPoint> CreateDataPointAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext context, CancellationToken token)
        {
            return Task.FromResult<IAsyncCodeLensDataPoint>(new GitCommitDataPoint(descriptor, context));
        }

        private class GitCommitDataPoint : IAsyncCodeLensDataPoint
        {
            private readonly CodeLensDescriptor descriptor;
            private readonly CodeLensDescriptorContext context;

            public GitCommitDataPoint(CodeLensDescriptor descriptor, CodeLensDescriptorContext context)
            {
                this.descriptor = descriptor;
                this.context = context;
            }

            public event AsyncEventHandler InvalidatedAsync;

            public CodeLensDescriptor Descriptor => this.descriptor;

            public async Task<CodeLensDataPointDescriptor> GetDataAsync(CodeLensDescriptorContext context, CancellationToken token)
            {
                return new CodeLensDataPointDescriptor
                {
                    Description = "disasmo",
                    TooltipText = "Show codegen code for this method/class",
                    IntValue = null,
                };
            }
            public Task<CodeLensDetailsDescriptor> GetDetailsAsync(CodeLensDescriptorContext context, CancellationToken token)
            {
                string str = "";
                foreach (var prop in context.Properties)
                {
                    str+= $"{prop.Key}={prop.Value}; ";
                }


                var result = new CodeLensDetailsDescriptor()
                {
                    
                    Headers = new [] { new CodeLensDetailHeaderDescriptor { Width = 2 } },
                    Entries = new [] { new CodeLensDetailEntryDescriptor { Fields = new List<CodeLensDetailEntryField> { new CodeLensDetailEntryField() } } },
                    CustomData = new List<string>()
                    {
                        "  "
                    }
                };
                return Task.FromResult(result);
            }

            public void Invalidate() => InvalidatedAsync?.Invoke(this, EventArgs.Empty).ConfigureAwait(false);
        }
    }
}
