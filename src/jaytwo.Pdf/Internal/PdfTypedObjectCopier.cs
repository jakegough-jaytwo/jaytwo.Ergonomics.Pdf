using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace jaytwo.Pdf.Internal;

// Copies an indirect graph through a structure-aware rule callback. The map includes the source store identity, and a
// destination reference is installed before children are visited so legitimate indirect cycles terminate.
internal sealed class PdfTypedObjectCopier
{
    private readonly Dictionary<SourceObject, PdfObject.Ref> _mapped = new Dictionary<SourceObject, PdfObject.Ref>();
    private readonly PdfNewFileWriter _writer;
    private readonly PdfNewFileBudget _budget;
    private readonly Func<CopyContext, CopyDecision> _policy;

    internal PdfTypedObjectCopier(PdfNewFileWriter writer, PdfNewFileBudget budget, Func<CopyContext, CopyDecision>? policy = null)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _budget = budget ?? throw new ArgumentNullException(nameof(budget));
        _policy = policy ?? (_ => CopyDecision.Copy());
    }

    internal enum CopyKind
    {
        Copy,
        Omit,
        Remap,
        Reject,
    }

    internal PdfObject.Ref Copy(PdfObjectStore source, PdfObject.Ref reference, string structure)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (structure == null)
        {
            throw new ArgumentNullException(nameof(structure));
        }

        return CopyReference(source, reference, structure, 0);
    }

    private PdfObject.Ref CopyReference(PdfObjectStore source, PdfObject.Ref reference, string structure, int depth)
    {
        CheckDepth(depth);
        var key = new SourceObject(source, reference.ObjectId, reference.GenerationNumber);
        if (_mapped.TryGetValue(key, out PdfObject.Ref? existing))
        {
            return existing;
        }

        PdfObject sourceValue = source.Resolve(reference);
        PdfObject destination = CreateShell(sourceValue);
        PdfObject.Ref destinationReference = _writer.Register(destination);
        _mapped.Add(key, destinationReference);
        _budget.CountMappedObject();
        Fill(source, sourceValue, destination, structure, depth + 1);
        return destinationReference;
    }

    private PdfObject CopyValue(PdfObjectStore source, PdfObject value, string structure, int depth)
    {
        CheckDepth(depth);
        if (value is PdfObject.Ref reference)
        {
            return CopyReference(source, reference, structure, depth + 1);
        }

        PdfObject destination = CreateShell(value);
        Fill(source, value, destination, structure, depth + 1);
        return destination;
    }

    private PdfObject CreateShell(PdfObject source)
    {
        if (source is PdfObject.Dict dict)
        {
            _budget.AddStructuralBytes(64 + (dict.Items.Count * 24L));
            return new PdfObject.Dict(dict.Items.Count);
        }

        if (source is PdfObject.Arr array)
        {
            _budget.AddStructuralBytes(32 + (array.Items.Count * 8L));
            return new PdfObject.Arr(array.Items.Count);
        }

        if (source is PdfObject.Str str)
        {
            _budget.AddStructuralBytes(24 + str.Raw.Length);
        }
        else if (source is PdfObject.Name name)
        {
            _budget.AddStructuralBytes(24 + (name.Value.Length * 2L));
        }
        else
        {
            _budget.AddStructuralBytes(24);
        }

        return source.DeepClone();
    }

    private void Fill(PdfObjectStore source, PdfObject sourceValue, PdfObject destination, string structure, int depth)
    {
        CheckDepth(depth);
        if (sourceValue is PdfObject.Dict sourceDictionary && destination is PdfObject.Dict destinationDictionary)
        {
            foreach ((string key, PdfObject value) in sourceDictionary.Items)
            {
                CopyDecision decision = _policy(new CopyContext(structure, key, sourceDictionary, value));
                if (decision.Kind == CopyKind.Omit)
                {
                    continue;
                }

                if (decision.Kind == CopyKind.Reject)
                {
                    throw new PdfException(decision.Message ?? ("The " + structure + " /" + key + " entry is unsupported."));
                }

                PdfObject copied = decision.Kind == CopyKind.Remap
                    ? decision.Value ?? throw new InvalidOperationException("A remap decision requires a value.")
                    : CopyValue(source, value, decision.ChildStructure ?? structure, depth + 1);
                destinationDictionary.Append(key, copied);
            }

            if (sourceDictionary.Body != null)
            {
                destinationDictionary.Body = new PdfObject.CopiedBody(() => source.OpenRawBody(sourceDictionary));
            }

            return;
        }

        if (sourceValue is PdfObject.Arr sourceArray && destination is PdfObject.Arr destinationArray)
        {
            foreach (PdfObject item in sourceArray.Items)
            {
                destinationArray.Items.Add(CopyValue(source, item, structure, depth + 1));
            }
        }
    }

    private void CheckDepth(int depth)
    {
        if (depth > _budget.TraversalDepth)
        {
            throw new PdfResourceLimitException("traversal depth");
        }
    }

    private readonly struct SourceObject : IEquatable<SourceObject>
    {
        private readonly PdfObjectStore _source;
        private readonly int _objectNumber;
        private readonly int _generation;

        internal SourceObject(PdfObjectStore source, int objectNumber, int generation)
        {
            _source = source;
            _objectNumber = objectNumber;
            _generation = generation;
        }

        public bool Equals(SourceObject other)
        {
            return ReferenceEquals(_source, other._source)
                && _objectNumber == other._objectNumber
                && _generation == other._generation;
        }

        public override bool Equals(object? obj)
        {
            return obj is SourceObject other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RuntimeHelpers.GetHashCode(_source);
                hash = (hash * 397) ^ _objectNumber;
                return (hash * 397) ^ _generation;
            }
        }
    }

    internal sealed class CopyContext
    {
        internal CopyContext(string structure, string key, PdfObject.Dict dictionary, PdfObject value)
        {
            Structure = structure;
            Key = key;
            Dictionary = dictionary;
            Value = value;
        }

        internal string Structure { get; }

        internal string Key { get; }

        internal PdfObject.Dict Dictionary { get; }

        internal PdfObject Value { get; }
    }

    internal sealed class CopyDecision
    {
        private CopyDecision(CopyKind kind, PdfObject? value, string? childStructure, string? message)
        {
            Kind = kind;
            Value = value;
            ChildStructure = childStructure;
            Message = message;
        }

        internal CopyKind Kind { get; }

        internal PdfObject? Value { get; }

        internal string? ChildStructure { get; }

        internal string? Message { get; }

        internal static CopyDecision Copy(string? childStructure = null)
        {
            return new CopyDecision(CopyKind.Copy, null, childStructure, null);
        }

        internal static CopyDecision Omit()
        {
            return new CopyDecision(CopyKind.Omit, null, null, null);
        }

        internal static CopyDecision Remap(PdfObject value)
        {
            return new CopyDecision(CopyKind.Remap, value, null, null);
        }

        internal static CopyDecision Reject(string message)
        {
            return new CopyDecision(CopyKind.Reject, null, null, message);
        }
    }
}
