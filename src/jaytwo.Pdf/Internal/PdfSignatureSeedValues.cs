using System;
using jaytwo.Pdf.Signatures;

namespace jaytwo.Pdf.Internal;

// ISO 32000-1 Table 234 and ISO 32000-2 Tables 237–238. W-151's owner disposition
// defines the noninteractive, timestamp, and arbitrary-provider interpretations.
internal sealed class PdfSignatureSeedValues
{
    private readonly PdfObjectStore _store;

    internal PdfSignatureSeedValues(PdfObjectStore store)
    {
        _store = store;
    }

    internal void Check(int fieldId, PdfSignatureOptions options)
    {
        var field = (PdfObject.Dict)_store.Load(fieldId);
        if (Get(field, "SV") is not PdfObject.Dict seed)
        {
            return;
        }

        long flags = Flags(seed);
        bool pdf20 = _store.EffectiveVersion() >= 20;
        if ((flags & 1) != 0 && Get(seed, "Filter") is PdfObject filter
            && PdfValues.NameOf(filter) != "Adobe.PPKLite")
        {
            Refuse("Filter");
        }

        if ((flags & 2) != 0 && Get(seed, "SubFilter") is PdfObject encodings)
        {
            CheckEncoding(encodings, options);
        }

        if ((flags & 4) != 0 && Get(seed, "V") is PdfObject version)
        {
            if (version is not PdfObject.Num number || (pdf20 && !number.IsInteger)
                || number.DoubleValue < 1 || number.DoubleValue > (pdf20 ? 3 : 2)
                || number.DoubleValue != Math.Truncate(number.DoubleValue))
            {
                Refuse("V");
            }
        }

        if ((flags & 8) != 0)
        {
            CheckReason(Get(seed, "Reasons"), options.Kind == PdfSignatureKind.DocTimeStamp ? null : options.Reason);
        }

        if ((flags & 16) != 0 && Get(seed, "LegalAttestation") != null)
        {
            Refuse("LegalAttestation");
        }

        if ((flags & 32) != 0 && Get(seed, "AddRevInfo") is PdfObject revocation
            && (revocation is not PdfObject.Bool value || value.Value))
        {
            Refuse("AddRevInfo");
        }

        if ((flags & 64) != 0 && Get(seed, "DigestMethod") is PdfObject digests)
        {
            CheckDigest(digests, options.DigestAlgorithm);
        }

        if (pdf20 && (flags & 128) != 0 && Get(seed, "LockDocument") is PdfObject locking)
        {
            CheckLock(locking, field);
        }

        if (pdf20 && (flags & 256) != 0 && Get(seed, "AppearanceFilter") != null)
        {
            Refuse("AppearanceFilter");
        }

        // These dictionaries have no top-level seed flag. Their own controls apply independently.
        if (Get(seed, "Cert") is PdfObject.Dict certificate)
        {
            CheckCertificate(certificate);
        }

        if (Get(seed, "MDP") is PdfObject.Dict mdp && Get(mdp, "P") is PdfObject permission)
        {
            if (permission is not PdfObject.Num p || !p.IsInteger || p.LongValue != 0
                || options.Kind == PdfSignatureKind.DocTimeStamp)
            {
                Refuse("MDP/P");
            }
        }

        if (Get(seed, "TimeStamp") is PdfObject.Dict timestamp)
        {
            long required = Flags(timestamp);
            if (required != 0 && (required != 1 || options.Kind != PdfSignatureKind.DocTimeStamp || Get(timestamp, "URL") != null))
            {
                Refuse("TimeStamp");
            }
        }
    }

    private void CheckEncoding(PdfObject value, PdfSignatureOptions options)
    {
        if (value is PdfObject.Arr array)
        {
            string selected = options.Kind == PdfSignatureKind.DocTimeStamp ? "ETSI.RFC3161"
                : options.Format == PdfSignatureFormat.PadesBaselineB ? "ETSI.CAdES.detached" : "adbe.pkcs7.detached";
            foreach (PdfObject item in array.Items)
            {
                string? name = PdfValues.NameOf(_store.Resolve(item));
                if (name == null)
                {
                    Refuse("SubFilter");
                }

                // Kind, not caller format preference, determines the supported set. Provider
                // capabilities are not discoverable through IPdfSignatureProvider.
                bool supported = options.Kind == PdfSignatureKind.DocTimeStamp ? name == "ETSI.RFC3161"
                    : name == "adbe.pkcs7.detached" || name == "ETSI.CAdES.detached";
                if (supported)
                {
                    if (name != selected)
                    {
                        Refuse("SubFilter");
                    }

                    return;
                }
            }
        }

        Refuse("SubFilter");
    }

    private void CheckDigest(PdfObject value, PdfDigestAlgorithm algorithm)
    {
        string digest = algorithm == PdfDigestAlgorithm.Sha384 ? "SHA384"
            : algorithm == PdfDigestAlgorithm.Sha512 ? "SHA512" : "SHA256";
        if (value is PdfObject.Arr array)
        {
            bool found = false;
            foreach (PdfObject item in array.Items)
            {
                string? name = PdfValues.NameOf(_store.Resolve(item));
                if (name == null)
                {
                    Refuse("DigestMethod");
                }

                found |= name == digest;
            }

            if (found)
            {
                return;
            }
        }

        // No supported operation justifies the mechanism-fixed-digest exception. An arbitrary
        // provider's unknown key type cannot establish the PDF 1.7 non-RSA exemption either.
        Refuse("DigestMethod");
    }

    private void CheckReason(PdfObject? value, string? reason)
    {
        if (value == null)
        {
            if (reason != null)
            {
                Refuse("Reasons");
            }

            return;
        }

        if (value is PdfObject.Arr array)
        {
            bool found = false;
            foreach (PdfObject item in array.Items)
            {
                if (_store.Resolve(item) is not PdfObject.Str text)
                {
                    Refuse("Reasons");
                    return;
                }

                string decoded = PdfTextCodec.Decode(text.Raw);
                if (array.Items.Count == 1 && decoded == ".")
                {
                    if (reason != null)
                    {
                        Refuse("Reasons");
                    }

                    return;
                }

                found |= string.Equals(decoded, reason, StringComparison.Ordinal);
            }

            if (found)
            {
                return;
            }
        }

        Refuse("Reasons");
    }

    private void CheckCertificate(PdfObject.Dict certificate)
    {
        long flags = Flags(certificate);
        string[] entries = { "Subject", "Issuer", "OID", "SubjectDN", "KeyUsage" };
        int[] masks = { 1, 2, 4, 8, 32 };
        for (int i = 0; i < entries.Length; i++)
        {
            if ((flags & masks[i]) != 0 && Get(certificate, entries[i]) != null)
            {
                Refuse("Cert/" + entries[i]);
            }
        }

        if ((flags & 64) != 0 && Get(certificate, "URL") != null
            && Get(certificate, "URLType") is PdfObject type && PdfValues.NameOf(type) != "Browser")
        {
            Refuse("Cert/URL");
        }

        // URLType Browser explicitly ignores the mandatory URL flag. Policy entries have
        // no policy-specific flag and remain advisory under the owner's W-151 disposition.
    }

    private void CheckLock(PdfObject value, PdfObject.Dict field)
    {
        string? name = PdfValues.NameOf(value);
        if (name == "auto")
        {
            return;
        }

        if (name == "false")
        {
            // A target field's /Lock/P introduces document permissions with this signature.
            // Field-only /Action locks and restrictions from earlier signatures stay intact.
            if (Get(field, "Lock") is not PdfObject.Dict fieldLock || Get(fieldLock, "P") == null)
            {
                return;
            }
        }

        Refuse("LockDocument");
    }

    private PdfObject? Get(PdfObject.Dict dictionary, string key)
    {
        PdfObject? value = _store.ResolveMaybe(dictionary.Get(key));
        return value is PdfObject.NullObj ? null : value;
    }

    private long Flags(PdfObject.Dict dictionary)
    {
        PdfObject? value = Get(dictionary, "Ff");
        if (value == null)
        {
            return 0;
        }

        if (value is PdfObject.Num number && number.IsInteger && number.LongValue >= 0)
        {
            return number.LongValue;
        }

        Refuse("Ff");
        return 0;
    }

    private void Refuse(string key)
    {
        throw new PdfSignException("The signature field's required seed value /" + key + " cannot be satisfied.");
    }
}
