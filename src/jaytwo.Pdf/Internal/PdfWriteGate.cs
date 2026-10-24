#pragma warning disable SA1201, SA1202, SA1204
using System.Collections.Generic;
using jaytwo.Pdf.AcroForm;

namespace jaytwo.Pdf.Internal;

// Decides whether a write may proceed: password permissions, DocMDP and FieldMDP certification, the signature checks
// flatten and rotation make, and the /UR3 usage rights a write keeps or removes.
internal sealed class PdfWriteGate
{
    private readonly PdfObjectStore _store;

    internal PdfWriteGate(PdfObjectStore store)
    {
        _store = store;
    }

    internal SignatureState? CachedSignatureState { get; set; }

    // Spec 7.2: flatten and rotate throw on a document with a signature unless the caller opts in.
    internal bool AllowBreakingSignatures { get; set; }

    internal void EnsureDocumentChange()
    {
        if (CertificationLevel() != 0)
        {
            throw new PdfCertificationLockedException();
        }
    }

    internal void EnsureAppendAllowed()
    {
        if (CertificationLevel() == 1)
        {
            throw new PdfCertificationLockedException();
        }
    }

    internal void EnsureSigningAllowed()
    {
        if (CertificationLevel() == 1)
        {
            throw new PdfCertificationLockedException();
        }
    }

    internal void RequireModify()
    {
        if (_store.Security != null && !_store.Security.CanModify)
        {
            throw new PdfEncryptionRestrictedException();
        }
    }

    internal void RequireFill()
    {
        if (_store.Security != null && !_store.Security.CanFill)
        {
            throw new PdfEncryptionRestrictedException();
        }
    }

    internal void RequireAnnotate()
    {
        if (_store.Security != null && !_store.Security.CanAnnotate)
        {
            throw new PdfEncryptionRestrictedException();
        }
    }

    // Spec 7.3: the catalog /Perms /UR3 usage-rights signature is kept only when its UR3 transform parameters
    // (ISO 32000-1 12.8.2.3, Table 255) grant the write: `right` is listed in the `category` array. A write that no
    // right grants (a null category), and a /UR3 whose transform is absent, does not resolve, is malformed, or is
    // unknown, removes /UR3 from /Perms in the same revision. On a document that already has a signature, no write
    // changes /Perms (spec 7.3): validators report every earlier signature as modified when /UR3 is removed. Callers
    // run this after every check that can refuse the write, and Sign runs it before it creates its own signature
    // dictionary, so a refused write still does not dirty the document; Sign restores /UR3 when signing fails.
    internal void ApplyUsageRights(string? category, string? right)
    {
        PdfObject? permsValue = _store.CatalogDict().Get("Perms");
        if (_store.ResolveMaybe(permsValue) is not PdfObject.Dict perms || perms.Get("UR3") is not PdfObject usageRights)
        {
            return;
        }

        if (category != null && right != null && UsageRightsGrant(usageRights, category, right))
        {
            return;
        }

        if (HasSignature())
        {
            return;
        }

        if (permsValue is PdfObject.Ref reference && _store.Load(reference.ObjectId) is PdfObject.Dict)
        {
            ((PdfObject.Dict)_store.EditAny(reference.ObjectId)).Remove("UR3");
            return;
        }

        var copy = (PdfObject.Dict)perms.DeepClone();
        copy.Remove("UR3");
        _store.EditCatalog().Set("Perms", copy);
    }

    // A document has a signature when /Perms has /DocMDP or a signature field has a /V that resolves to a dictionary,
    // the test Sign uses to refuse an already signed field. A /V that does not resolve is null (ISO 32000-1 7.3.10),
    // so that field is unsigned. The /UR3 usage-rights signature is not counted.
    internal bool HasSignature()
    {
        return GetSignatureState().HasSignature;
    }

    private bool UsageRightsGrant(PdfObject usageRights, string category, string right)
    {
        if (_store.Resolve(usageRights) is not PdfObject.Dict signature
            || _store.ResolveMaybe(signature.Get("Reference")) is not PdfObject.Arr references
            || references.Items.Count == 0)
        {
            return false;
        }

        foreach (PdfObject item in references.Items)
        {
            if (_store.Resolve(item) is not PdfObject.Dict reference
                || PdfValues.NameOf(_store.ResolveMaybe(reference.Get("TransformMethod"))) != "UR3"
                || _store.ResolveMaybe(reference.Get("TransformParams")) is not PdfObject.Dict parameters
                || !IsUsageRightsVersion(parameters.Get("V"))
                || !ListsRight(_store.ResolveMaybe(parameters.Get(category)), right))
            {
                return false;
            }
        }

        return true;
    }

    // ISO 32000-1 Table 255: /V is the name 2.2, which is also its default. An unknown version enables no rights.
    private bool IsUsageRightsVersion(PdfObject? value)
    {
        if (value == null)
        {
            return true;
        }

        return !_store.IsDangling(value) && PdfValues.NameOf(_store.Resolve(value)) == "2.2";
    }

    private bool ListsRight(PdfObject? value, string right)
    {
        if (value is not PdfObject.Arr rights)
        {
            return false;
        }

        bool found = false;
        foreach (PdfObject item in rights.Items)
        {
            string? name = PdfValues.NameOf(_store.Resolve(item));
            if (name == null)
            {
                return false;
            }

            found |= name == right;
        }

        return found;
    }

    internal int CertificationLevel()
    {
        return GetSignatureState().CertificationLevel;
    }

    internal int CertificationLevel(List<FieldSlot> slots)
    {
        if (CachedSignatureState != null)
        {
            return CachedSignatureState.CertificationLevel;
        }

        SignatureState state = CreateSignatureState();
        foreach (FieldSlot slot in slots)
        {
            if (slot.FieldType != PdfFieldType.Signature)
            {
                continue;
            }

            if (HasSignatureDictionary(slot))
            {
                state.HasSignature = true;
            }

            if (_store.Load(slot.ValueObjectId) is PdfObject.Dict field
                && _store.ResolveMaybe(field.Get("V")) is PdfObject.Dict signature)
            {
                int level = state.CertificationLevel;
                Restrict(ref level, PermissionOnSignature(signature, "DocMDP"));
                state.CertificationLevel = level;
            }
        }

        CachedSignatureState = state;
        return state.CertificationLevel;
    }

    private SignatureState GetSignatureState()
    {
        if (CachedSignatureState != null)
        {
            return CachedSignatureState;
        }

        SignatureState state = CreateSignatureState();
        PdfObject.Dict catalog = _store.CatalogDict();
        if (_store.ResolveMaybe(catalog.Get("AcroForm")) is PdfObject.Dict acro
            && _store.ResolveMaybe(acro.Get("Fields")) is PdfObject.Arr fields)
        {
            var seen = new HashSet<int>();
            foreach (PdfObject field in fields.Items)
            {
                if (field is PdfObject.Ref reference && _store.Resolve(reference) is PdfObject.Dict dict)
                {
                    WalkSignatures(dict, reference.ObjectId, null, 0, state, seen, 0);
                }
            }
        }

        CachedSignatureState = state;
        return state;
    }

    private SignatureState CreateSignatureState()
    {
        var state = new SignatureState();
        PdfObject? permsValue = _store.CatalogDict().Get("Perms");
        if (_store.IsDangling(permsValue))
        {
            int level = state.CertificationLevel;
            Restrict(ref level, 1);
            state.CertificationLevel = level;
        }
        else if (_store.ResolveMaybe(permsValue) is PdfObject.Dict perms && perms.Get("DocMDP") != null)
        {
            state.HasSignature = true;
            int level = state.CertificationLevel;
            Restrict(ref level, PermissionOf(perms.Get("DocMDP")));
            state.CertificationLevel = level;
        }

        return state;
    }

    private void WalkSignatures(
        PdfObject.Dict field,
        int fieldId,
        string? inheritedType,
        int inheritedValueOwner,
        SignatureState state,
        HashSet<int> seen,
        int depth)
    {
        if (!seen.Add(fieldId))
        {
            throw new PdfParseException(0, "The field tree contains a cycle.");
        }

        if (depth > PdfObjectStore.MaxTraversalDepth)
        {
            throw new PdfParseException(0, "The field tree is too deep.");
        }

        bool isField = inheritedValueOwner == 0 || field.Get("T") != null;
        string? fieldType = PdfValues.NameOf(field.Get("FT")) ?? inheritedType;
        int valueOwner = isField ? fieldId : inheritedValueOwner;
        if (_store.ResolveMaybe(field.Get("Kids")) is PdfObject.Arr kids && kids.Items.Count > 0)
        {
            foreach (PdfObject kid in kids.Items)
            {
                if (kid is PdfObject.Ref reference && _store.Resolve(reference) is PdfObject.Dict child)
                {
                    WalkSignatures(child, reference.ObjectId, fieldType, valueOwner, state, seen, depth + 1);
                }
            }

            return;
        }

        bool widget = PdfValues.NameOf(field.Get("Subtype")) == "Widget" || field.Get("Rect") != null;
        if (!widget || fieldType != "Sig")
        {
            return;
        }

        if (_store.Load(valueOwner) is PdfObject.Dict owner && _store.ResolveMaybe(owner.Get("V")) is PdfObject.Dict signature)
        {
            state.HasSignature = true;
            int level = state.CertificationLevel;
            Restrict(ref level, PermissionOnSignature(signature, "DocMDP"));
            state.CertificationLevel = level;
        }
        else if (NearestFieldValue(fieldId) is PdfObject.Dict)
        {
            state.HasSignature = true;
        }
    }

    private int PermissionOf(PdfObject? value)
    {
        if (value is not PdfObject.Ref reference || !_store.Xref.ContainsKey(reference.ObjectId))
        {
            return 1;
        }

        if (_store.ResolveMaybe(reference) is not PdfObject.Dict signature)
        {
            return 1;
        }

        int permission = PermissionOnSignature(signature, "DocMDP");
        return permission == 0 ? 1 : permission;
    }

    internal int PermissionOnSignature(PdfObject.Dict signature, string method)
    {
        if (_store.IsDangling(signature.Get("Reference")))
        {
            return 1;
        }

        if (_store.ResolveMaybe(signature.Get("Reference")) is not PdfObject.Arr references)
        {
            return 0;
        }

        int found = 0;
        foreach (PdfObject item in references.Items)
        {
            if (_store.IsDangling(item))
            {
                return 1;
            }

            if (_store.ResolveMaybe(item) is not PdfObject.Dict reference || PdfValues.NameOf(reference.Get("TransformMethod")) != method)
            {
                continue;
            }

            int permission = ReadPermission(reference);
            if (found == 0 || permission < found)
            {
                found = permission;
            }
        }

        return found;
    }

    private int ReadPermission(PdfObject.Dict reference)
    {
        PdfObject? parameters = reference.Get("TransformParams");
        if (parameters == null)
        {
            return 2;
        }

        if (_store.IsDangling(parameters))
        {
            return 1;
        }

        PdfObject.Dict? dict = parameters as PdfObject.Dict ?? _store.ResolveMaybe(parameters) as PdfObject.Dict;
        if (dict == null)
        {
            return 1;
        }

        if (dict.Get("P") == null)
        {
            return 2;
        }

        int permission = PdfValues.IntOf(dict.Get("P"));
        return permission is >= 1 and <= 3 ? permission : 1;
    }

    private static void Restrict(ref int level, int permission)
    {
        if (permission == 0)
        {
            return;
        }

        if (permission < 1 || permission > 3)
        {
            permission = 1;
        }

        if (level == 0 || permission < level)
        {
            level = permission;
        }
    }

    internal bool FieldChangeBlocked(string fullName, List<FieldSlot> slots)
    {
        foreach (FieldSlot slot in slots)
        {
            if (slot.FieldType != PdfFieldType.Signature)
            {
                continue;
            }

            if (_store.Load(slot.ValueObjectId) is not PdfObject.Dict field || _store.ResolveMaybe(field.Get("V")) is not PdfObject.Dict signature)
            {
                continue;
            }

            if (_store.IsDangling(signature.Get("Reference")))
            {
                return true;
            }

            if (_store.ResolveMaybe(signature.Get("Reference")) is not PdfObject.Arr references)
            {
                continue;
            }

            foreach (PdfObject item in references.Items)
            {
                if (_store.IsDangling(item))
                {
                    return true;
                }

                if (_store.ResolveMaybe(item) is not PdfObject.Dict reference || PdfValues.NameOf(reference.Get("TransformMethod")) != "FieldMDP")
                {
                    continue;
                }

                if (FieldMdpBlocks(reference, fullName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool FieldMdpBlocks(PdfObject.Dict reference, string fullName)
    {
        PdfObject? parameters = reference.Get("TransformParams");
        if (parameters == null || _store.IsDangling(parameters))
        {
            return true;
        }

        PdfObject.Dict? dict = parameters as PdfObject.Dict ?? _store.ResolveMaybe(parameters) as PdfObject.Dict;
        if (dict == null)
        {
            return true;
        }

        string action = PdfValues.NameOf(dict.Get("Action")) ?? "All";
        if (action == "All")
        {
            return true;
        }

        PdfObject? fieldValues = dict.Get("Fields");
        if (fieldValues == null || _store.IsDangling(fieldValues) || _store.ResolveMaybe(fieldValues) is not PdfObject.Arr fields)
        {
            return true;
        }

        var names = new HashSet<string>();
        foreach (PdfObject field in fields.Items)
        {
            if (_store.IsDangling(field))
            {
                return true;
            }

            string? name = _store.ValueText(field);
            if (name == null)
            {
                return true;
            }

            names.Add(name);
        }

        // ISO 32000-1 12.8.2.4: Include locks only the named fields; Exclude locks every field except them.
        // A named field covers its descendants, as pyHanko applies it. An unknown action locks, like an
        // unresolved certification.
        if (action == "Include")
        {
            return Names(names, fullName);
        }

        return action != "Exclude" || !Names(names, fullName);
    }

    private static bool Names(HashSet<string> names, string fullName)
    {
        if (names.Contains(fullName))
        {
            return true;
        }

        for (int dot = fullName.LastIndexOf('.'); dot > 0; dot = fullName.LastIndexOf('.', dot - 1))
        {
            if (names.Contains(fullName.Substring(0, dot)))
            {
                return true;
            }
        }

        return false;
    }

    internal bool HasSignatureDictionary(FieldSlot slot)
    {
        if (_store.Load(slot.ValueObjectId) is PdfObject.Dict owner && _store.ResolveMaybe(owner.Get("V")) is PdfObject.Dict)
        {
            return true;
        }

        return NearestFieldValue(slot.WidgetObjectId) is PdfObject.Dict;
    }

    private PdfObject? NearestFieldValue(int objectId)
    {
        var seen = new HashSet<int>();
        while (seen.Add(objectId))
        {
            if (_store.Load(objectId) is not PdfObject.Dict dict)
            {
                return null;
            }

            PdfObject? value = dict.Get("V");
            if (value != null)
            {
                return _store.ResolveMaybe(value);
            }

            if (dict.Get("Parent") is not PdfObject.Ref parent || !_store.IsLive(parent))
            {
                return null;
            }

            objectId = parent.ObjectId;
        }

        return null;
    }
}
