// Test-only adapter for the pinned DSS 6.3 API. No signing or validation code from the product is used here.
import com.fasterxml.jackson.databind.ObjectMapper;
import eu.europa.esig.dss.diagnostic.*;
import eu.europa.esig.dss.model.FileDocument;
import eu.europa.esig.dss.pades.validation.PDFDocumentValidator;
import eu.europa.esig.dss.spi.DSSUtils;
import eu.europa.esig.dss.spi.validation.CommonCertificateVerifier;
import eu.europa.esig.dss.spi.x509.CommonTrustedCertificateSource;
import eu.europa.esig.dss.spi.x509.revocation.crl.ExternalResourcesCRLSource;
import java.nio.file.*;
import java.time.Instant;
import java.util.*;

class DssValidate {
    // input, output directory, trust/CRL directory, validation instant, password ("-" for none)
    public static void main(String[] args) throws Exception {
        Path output = Path.of(args[1]);
        Files.createDirectories(output);
        var result = new LinkedHashMap<String, Object>();
        var version = new Properties();
        try (var stream = DssValidate.class.getResourceAsStream("/META-INF/maven/eu.europa.ec.joinup.sd-dss/dss-pades/pom.properties")) {
            version.load(stream);
        }
        result.put("dss", version.getProperty("version"));
        result.put("java", System.getProperty("java.runtime.version"));
        result.put("os", System.getProperty("os.name") + " " + System.getProperty("os.version"));
        result.put("validationTime", args[3]);
        result.put("network", "disabled: no AIA, OCSP, online CRL, or trusted-list sources");
        try {
            if (!"6.3".equals(version.getProperty("version")) || !System.getProperty("java.runtime.version").startsWith("21.0.9+10")) {
                throw new IllegalStateException("DSS 6.3 and JDK 21.0.9+10 are required");
            }
            var trusted = new CommonTrustedCertificateSource();
            var crls = new ArrayList<FileDocument>();
            try (var files = Files.list(Path.of(args[2]))) {
                for (Path path : files.sorted().toList()) {
                    if (path.toString().endsWith(".cer")) trusted.addCertificate(DSSUtils.loadCertificate(path.toFile()));
                    if (path.toString().endsWith(".crl")) crls.add(new FileDocument(path.toFile()));
                }
            }
            var verifier = new CommonCertificateVerifier(true); // no default online AIA source
            verifier.setTrustedCertSources(trusted);
            verifier.setCrlSource(new ExternalResourcesCRLSource(crls.toArray(FileDocument[]::new)));
            verifier.setOcspSource(null);
            verifier.setAIASource(null);
            var validator = new PDFDocumentValidator(new FileDocument(args[0]));
            if (!args[4].equals("-")) validator.setPasswordProtection(args[4].toCharArray());
            validator.setCertificateVerifier(verifier);
            validator.setValidationTime(Date.from(Instant.parse(args[3])));
            Path policy = output.resolve("policy.xml");
            try (var stream = DssValidate.class.getResourceAsStream("/policy/constraint.xml")) {
                if (stream == null) throw new IllegalStateException("Pinned DSS default policy missing");
                Files.copy(stream, policy, StandardCopyOption.REPLACE_EXISTING);
            }
            var reports = validator.validateDocument(policy.toFile());
            Files.writeString(output.resolve("simple.xml"), reports.getXmlSimpleReport());
            Files.writeString(output.resolve("detailed.xml"), reports.getXmlDetailedReport());
            Files.writeString(output.resolve("diagnostic.xml"), reports.getXmlDiagnosticData());
            Files.writeString(output.resolve("validation.xml"), reports.getXmlValidationReport());
            var simple = reports.getSimpleReport();
            var signatures = new ArrayList<Object>();
            for (String id : simple.getSignatureIdList()) {
                var signature = reports.getDiagnosticData().getSignatureById(id);
                var entry = new LinkedHashMap<String, Object>();
                entry.put("id", id);
                entry.put("fieldNames", signature.getSignatureFieldNames());
                entry.put("profile", simple.getSignatureFormat(id));
                entry.put("digest", signature.getDigestAlgorithm());
                entry.put("encryption", signature.getEncryptionAlgorithm());
                entry.put("signatureIntact", signature.isSignatureIntact());
                entry.put("signatureValid", signature.isSignatureValid());
                entry.put("digestIntact", signature.getMessageDigest().isDataIntact());
                entry.put("trustedChain", signature.isTrustedChain());
                entry.put("indication", simple.getIndication(id));
                entry.put("subIndication", simple.getSubIndication(id));
                var timestamps = new ArrayList<Object>();
                for (var stamp : signature.getSignatureTimestamps()) {
                    var ts = new LinkedHashMap<String, Object>();
                    ts.put("id", stamp.getId());
                    ts.put("signatureIntact", stamp.isSignatureIntact());
                    ts.put("signatureValid", stamp.isSignatureValid());
                    ts.put("imprintIntact", stamp.isMessageImprintDataIntact());
                    ts.put("trustedChain", stamp.isTrustedChain());
                    ts.put("indication", simple.getIndication(stamp.getId()));
                    ts.put("subIndication", simple.getSubIndication(stamp.getId()));
                    timestamps.add(ts);
                }
                entry.put("timestamps", timestamps);
                signatures.add(entry);
            }
            result.put("signatures", signatures);
            result.put("execution", "completed");
        } catch (Exception exception) {
            result.put("execution", "error");
            result.put("errorType", exception.getClass().getName());
            result.put("error", exception.toString());
            exception.printStackTrace();
            new ObjectMapper().writerWithDefaultPrettyPrinter().writeValue(output.resolve("result.json").toFile(), result);
            System.exit(2);
        }
        new ObjectMapper().writerWithDefaultPrettyPrinter().writeValue(output.resolve("result.json").toFile(), result);
    }
}
