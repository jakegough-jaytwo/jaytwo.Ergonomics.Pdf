"""Extract only the official DSS web application's libraries; never start Tomcat."""
import io
import pathlib
import sys
import zipfile

bundle, destination = sys.argv[1:]
target = pathlib.Path(destination)
target.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(bundle) as archive:
    war_name = "dss-demo-bundle-6.3/apache-tomcat-11.0.9/webapps/ROOT.war"
    with zipfile.ZipFile(io.BytesIO(archive.read(war_name))) as war:
        for name in war.namelist():
            if name.startswith("WEB-INF/lib/") and name.endswith(".jar"):
                (target / pathlib.PurePosixPath(name).name).write_bytes(war.read(name))
