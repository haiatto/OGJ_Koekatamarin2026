using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace Koekatamarin.Editor
{
    public sealed class AndroidPermissions : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 10000;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var file = Path.Combine(path, "src/main/AndroidManifest.xml");
            var document = new XmlDocument();
            document.Load(file);
            const string android = "http://schemas.android.com/apk/res/android";
            foreach (var permission in new[] { "android.permission.INTERNET", "android.permission.RECORD_AUDIO",
                "android.permission.CHANGE_WIFI_MULTICAST_STATE" })
            {
                bool found = false;
                foreach (XmlElement element in document.SelectNodes("/manifest/uses-permission"))
                    if (element.GetAttribute("name", android) == permission) found = true;
                if (found) continue;
                var node = document.CreateElement("uses-permission");
                node.SetAttribute("name", android, permission);
                document.DocumentElement.AppendChild(node);
            }
            document.Save(file);
        }
    }
}
