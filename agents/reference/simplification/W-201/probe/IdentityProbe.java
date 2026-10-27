import java.io.File;
import java.nio.file.*;
import org.apache.pdfbox.Loader;
import org.apache.pdfbox.pdmodel.PDDocument;
import org.apache.pdfbox.multipdf.PDFMergerUtility;
import org.apache.pdfbox.multipdf.Splitter;
import org.apache.pdfbox.io.IOUtils;
import org.apache.pdfbox.pdfwriter.compress.CompressParameters;
public class IdentityProbe {
    public static void main(String[] args) throws Exception {
        var base = Path.of(args[0]);
        String[] singles = {"identity-a", "identity-b", "minimal", "malformed", "custom"};
        for (var name : singles) {
            try (var doc = Loader.loadPDF(base.resolve("inputs/"+name+".pdf").toFile())) {
                var split = new Splitter().split(doc);
                try (var selected = split.get(0)) {
                    selected.save(base.resolve("outputs/"+name+"-pdfbox-split.pdf").toFile(), CompressParameters.NO_COMPRESSION);
                }
            }
        }
        String[] names = {"identity-a", "identity-b", "minimal", "malformed", "custom", "a-b", "b-a", "a-a"};
        for (var name : names) for (var mode : PDFMergerUtility.DocumentMergeMode.values()) {
            var sources = switch(name) { case "a-b" -> new String[]{"identity-a","identity-b"}; case "b-a" -> new String[]{"identity-b","identity-a"}; case "a-a" -> new String[]{"identity-a","identity-a"}; default -> new String[]{name}; };
            var merger = new PDFMergerUtility(); merger.setDocumentMergeMode(mode);
            for (var source : sources) merger.addSource(base.resolve("inputs/"+source+".pdf").toFile());
            merger.setDestinationFileName(base.resolve("outputs/"+name+"-pdfbox-"+mode+".pdf").toString());
            merger.mergeDocuments(IOUtils.createMemoryOnlyStreamCache(), CompressParameters.NO_COMPRESSION);
            System.out.println(name+" "+mode+" accepted");
        }
    }
}
