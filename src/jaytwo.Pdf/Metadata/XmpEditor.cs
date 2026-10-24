using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace jaytwo.Pdf.Metadata;

internal static class XmpEditor
{
    internal const int MaxDocumentBytes = 32 * 1024 * 1024;

    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Pdf = "http://ns.adobe.com/pdf/1.3/";
    private static readonly XNamespace Xmp = "http://ns.adobe.com/xap/1.0/";
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace X = "adobe:ns:meta/";

    public static bool TryParse(byte[] utf8, out XDocument? document, out Exception? error)
    {
        try
        {
            if (utf8.Length > MaxDocumentBytes)
            {
                throw new XmlException("XMP exceeds the 32 MiB document limit.");
            }

            using var stream = new MemoryStream(utf8, false);
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersFromEntities = 0,
                MaxCharactersInDocument = MaxDocumentBytes,
                XmlResolver = null,
            };
            using var reader = XmlReader.Create(stream, settings);
            document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            document = null;
            error = ex;
            return false;
        }
    }

    public static byte[] ToBytes(XDocument document)
    {
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = document.Declaration == null,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
        };
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings))
        {
            document.Save(writer);
        }

        return stream.ToArray();
    }

    public static XDocument CreateMinimal()
    {
        var description = new XElement(
            Rdf + "Description",
            new XAttribute(Rdf + "about", string.Empty),
            new XAttribute(XNamespace.Xmlns + "dc", Dc.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "pdf", Pdf.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xmp", Xmp.NamespaceName));
        return new XDocument(
            new XProcessingInstruction("xpacket", "begin=\"\uFEFF\" id=\"W5M0MpCehiHzreSzNTczkc9d\""),
            new XElement(
                X + "xmpmeta",
                new XAttribute(XNamespace.Xmlns + "x", X.NamespaceName),
                new XElement(
                    Rdf + "RDF",
                    new XAttribute(XNamespace.Xmlns + "rdf", Rdf.NamespaceName),
                    description)),
            new XProcessingInstruction("xpacket", "end=\"w\""));
    }

    public static void ApplyInfo(XDocument document, string key, string? value)
    {
        switch (key)
        {
            case "Title":
                SetAlt(document, Dc + "title", value);
                break;
            case "Subject":
                SetAlt(document, Dc + "description", value);
                break;
            case "Author":
                SetSeq(document, Dc + "creator", value);
                break;
            case "Keywords":
                SetSimple(document, Pdf + "Keywords", value);
                break;
            case "Producer":
                SetSimple(document, Pdf + "Producer", value);
                break;
            case "Creator":
                SetSimple(document, Xmp + "CreatorTool", value);
                break;
            case "CreationDate":
                SetSimple(document, Xmp + "CreateDate", value);
                break;
            case "ModDate":
                SetSimple(document, Xmp + "ModifyDate", value);
                break;
            default:
                break;
        }
    }

    private static void SetSimple(XDocument document, XName name, string? value)
    {
        XElement description = EnsureDescription(document);
        XAttribute? attribute = description.Attribute(name);
        XElement? element = description.Element(name);
        if (value == null)
        {
            attribute?.Remove();
            element?.Remove();
            return;
        }

        if (attribute != null)
        {
            attribute.Value = value;
            return;
        }

        if (element == null)
        {
            element = new XElement(name);
            description.Add(element);
        }

        element.Value = value;
    }

    private static void SetAlt(XDocument document, XName name, string? value)
    {
        XElement description = EnsureDescription(document);
        XAttribute? attribute = description.Attribute(name);
        if (attribute != null && value != null)
        {
            attribute.Value = value;
            return;
        }

        XElement? element = description.Element(name);
        if (value == null)
        {
            attribute?.Remove();
            element?.Remove();
            return;
        }

        if (element == null)
        {
            element = new XElement(
                name,
                new XElement(
                    Rdf + "Alt",
                    new XElement(
                        Rdf + "li",
                        new XAttribute(XNamespace.Xml + "lang", "x-default"),
                        value)));
            description.Add(element);
            return;
        }

        XElement? li = element.Descendants(Rdf + "li").FirstOrDefault();
        if (li == null)
        {
            element.Add(new XElement(
                Rdf + "Alt",
                new XElement(Rdf + "li", new XAttribute(XNamespace.Xml + "lang", "x-default"), value)));
            return;
        }

        li.Value = value;
    }

    private static void SetSeq(XDocument document, XName name, string? value)
    {
        XElement description = EnsureDescription(document);
        XElement? element = description.Element(name);
        if (value == null)
        {
            element?.Remove();
            return;
        }

        if (element == null)
        {
            element = new XElement(name, new XElement(Rdf + "Seq", new XElement(Rdf + "li", value)));
            description.Add(element);
            return;
        }

        XElement? li = element.Descendants(Rdf + "li").FirstOrDefault();
        if (li == null)
        {
            element.Add(new XElement(Rdf + "Seq", new XElement(Rdf + "li", value)));
            return;
        }

        li.Value = value;
    }

    private static XElement EnsureDescription(XDocument document)
    {
        XElement? description = document.Descendants(Rdf + "Description").FirstOrDefault();
        if (description != null)
        {
            description.SetAttributeValue(XNamespace.Xmlns + "dc", Dc.NamespaceName);
            description.SetAttributeValue(XNamespace.Xmlns + "pdf", Pdf.NamespaceName);
            description.SetAttributeValue(XNamespace.Xmlns + "xmp", Xmp.NamespaceName);
            return description;
        }

        XElement rdf = document.Descendants(Rdf + "RDF").FirstOrDefault() ?? throw new PdfXmpException("XMP has no rdf:RDF element.");
        description = new XElement(
            Rdf + "Description",
            new XAttribute(Rdf + "about", string.Empty),
            new XAttribute(XNamespace.Xmlns + "dc", Dc.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "pdf", Pdf.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xmp", Xmp.NamespaceName));
        rdf.Add(description);
        return description;
    }
}
