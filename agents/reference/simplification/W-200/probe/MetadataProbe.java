import java.io.File;
import java.nio.file.*;
import org.apache.pdfbox.Loader;
import org.apache.pdfbox.pdmodel.PDDocument;
public class MetadataProbe {
    public static void main(String[] args) throws Exception {
        try (var files = Files.list(Path.of(args[0], "inputs"))) {
            for (var path : files.sorted().toList()) {
                var name = path.getFileName().toString();
                if (name.startsWith("restricted") || name.startsWith("gate-")) continue;
                try (PDDocument doc = Loader.loadPDF(path.toFile())) {
                    doc.getDocumentInformation().setTitle("W200 日本語");
                    doc.save(new File(args[0], "outputs/" + name.replace(".pdf", "-pdfbox.pdf")));
                    System.out.println(name + " accepted");
                } catch (Exception e) { System.out.println(name + " refused " + e.getClass().getSimpleName()); }
            }
        }
    }
}
