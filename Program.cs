using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.CommandLine;
using System.Text.RegularExpressions;

namespace QuickbaseFileDownloader
{
    public class RecordQueryRequest
    {
        public string from { get; set; }

        public List<int> select { get; set; }

        public string? where { get; set; }
    }

    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var tokenOption = new Option<string>(
                name: "--qb-user-token",
                description: "The Quickbase User Token to be used."
            );

            var realmOption = new Option<string>(
                name: "--qb-realm",
                description: "The Quickbase realm URL."
            );

            var tableOption = new Option<string>(
                name: "--qb-table-id",
                description: "The Quickbase target table ID."
            );

            var pathOption = new Option<string>(
                name: "--working-path",
                description: "The destination path to store the files.",
                getDefaultValue: () => @"C:\temp\Quickbase Files\"
            );

            var fieldFolderOption = new Option<bool>(
                name: "--field-label-as-folder-name",
                description: "Use the attachment field name as the containing folder name, rather than the ID."
            );

            var fieldFolderOptionClean = new Option<bool>(
                name: "--clean-field-label-as-folder-name",
                description: "Use the attachment field name as the containing folder name, rather than the ID. Special characters removed."
            );

            var rootCommand = new RootCommand(
                "Utility to download file attachments from a Quickbase table."
            );
            rootCommand.AddOption(tokenOption);
            rootCommand.AddOption(realmOption);
            rootCommand.AddOption(tableOption);
            rootCommand.AddOption(pathOption);
            rootCommand.AddOption(fieldFolderOption);
            rootCommand.AddOption(fieldFolderOptionClean);

            rootCommand.SetHandler(
                (path, token, realm, table, fieldFolder, cleanFieldFolder) =>
                {
                    DownloadFiles(path, token!, realm!, table!, fieldFolder, cleanFieldFolder);
                },
                pathOption,
                tokenOption,
                realmOption,
                tableOption,
                fieldFolderOption,
                fieldFolderOptionClean
            );

            return await rootCommand.InvokeAsync(args);
        }

        private static HttpClient qbClient =
            new() { BaseAddress = new Uri("https://api.quickbase.com/v1"), };

        public static void DownloadFiles(
            string workingPath,
            string qbUserToken,
            string qbRealmHostname,
            string tableId,
            bool fieldLabelAsFolderName,
            bool cleanFieldLabelAsFolderName
        )
        {
            const string fieldMetadataBaseUrl = "https://api.quickbase.com/v1/fields";
            const string recordQueryBaseUrl = "https://api.quickbase.com/v1/records/query";
            const string fileDownloadBaseUrl = "https://api.quickbase.com/v1/files/";
            // string workingPath;
            // string qbUserToken;
            // string qbRealmHostname;
            // string tableId;

            if (string.IsNullOrEmpty(workingPath))
            {
                workingPath = @"C:\temp\Quickbase Files\";
            }

            // int recordId = 1277;

            // HttpClient fieldDataClient = new HttpClient();

            // fieldDataClient.DefaultRequestHeaders.Add(
            //     "Authorization",
            //     "QB-USER-TOKEN " + qbUserToken
            // );
            // fieldDataClient.DefaultRequestHeaders.Add("QB-Realm-Hostname", qbRealmHostname);

            qbClient.DefaultRequestHeaders.Add(
                "Authorization",
                "QB-USER-TOKEN " + qbUserToken
            );
            qbClient.DefaultRequestHeaders.Add("QB-Realm-Hostname", qbRealmHostname);

            // Uri fieldMetadataUrl = new System.Uri(fieldMetadataBaseUrl);

            // string fieldMetadataUrl = fieldMetadataBaseUrl + "?tableId=" + tableId;
            string fieldMetadataUrl = "/fields/?tableId=" + tableId;

            // var fieldMetadataResponse = fieldDataClient.GetAsync(fieldMetadataUrl).Result;
            // var fieldMetadataResponse = fieldDataClient.GetStringAsync(fieldMetadataUrl).Result;
            var fieldMetadataResponse = qbClient.GetStringAsync(fieldMetadataUrl).Result;
            var fieldMetadata = string.Empty;

            if (!string.IsNullOrWhiteSpace(fieldMetadataResponse))
            {
                fieldMetadata = fieldMetadataResponse;
            }
            else
            {
                throw new Exception("Error in getting table field data");
            }

            // fieldDataClient.Dispose();

            JsonNode fieldMetadataNode = JsonNode.Parse(fieldMetadata)!;

            Dictionary<int, string> attachmentFields = new Dictionary<int, string>();

            foreach (var field in fieldMetadataNode.AsArray())
            {
                string fieldType = field!["fieldType"]!.ToString()!;

                if (fieldType == "file")
                {
                    attachmentFields.Add(
                        field!["id"]!.GetValue<int>()!,
                        field!["label"]!.GetValue<string>()!
                    );
                }
            }

            var serializerOptions = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                // WriteIndented = true
            };

            // HttpClient jsonClient = new HttpClient();

            // jsonClient.DefaultRequestHeaders.Accept.Add(
            //     new MediaTypeWithQualityHeaderValue("application/json")
            // );
            // jsonClient.DefaultRequestHeaders.Add("Authorization", "QB-USER-TOKEN " + qbUserToken);
            // jsonClient.DefaultRequestHeaders.Add("QB-Realm-Hostname", qbRealmHostname);

            qbClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
            );

            List<int> fieldIdsToReturn = attachmentFields.Keys.ToList();

            // fieldIdsToReturn.Prepend<int>(3);

            fieldIdsToReturn.Add(3);

            string fieldRequestJsonString = System.Text.Json.JsonSerializer.Serialize(
                new RecordQueryRequest
                {
                    from = tableId,
                    // select = new int[] { 36, 37, 38, 39, 40, 41, 42, 57, 58, 59, 60, 61, 55, 56 },
                    // select = attachmentFields.Keys.ToArray(),
                    select = fieldIdsToReturn
                    // where = $"{{3.EX.'{recordId}'}}"
                },
                serializerOptions
            );

            StringContent fieldRequestContent = new StringContent(
                fieldRequestJsonString,
                Encoding.UTF8,
                "application/json"
            );

            // var recordQueryResponse = jsonClient
            //     .PostAsync(recordQueryBaseUrl, fieldRequestContent)
            //     .Result;
            var recordQueryResponse = qbClient
                .PostAsync("/records/query", fieldRequestContent)
                .Result;

            var fieldData = string.Empty;

            if (recordQueryResponse.IsSuccessStatusCode)
            {
                fieldData = recordQueryResponse.Content.ReadAsStringAsync().Result;
            }
            else
            {
                throw new Exception("Error in getting record data");
            }

            // jsonClient.Dispose();

            // System.IO.Directory.CreateDirectory(
            //                             workingPath
            //                         );

            // System.Diagnostics.Process.Start(workingPath);

            // Console.WriteLine(fieldData);

            JsonNode fieldDataNode = JsonNode.Parse(fieldData)!;

            foreach (var record in fieldDataNode!["data"]!.AsArray()!)
            {
                Console.WriteLine(record!["3"]!["value"]!.GetValue<int>()!);

                int recordId = record!["3"]!["value"]!.GetValue<int>()!;

                foreach (var field in fieldDataNode!["fields"]!.AsArray()!)
                {
                    int fieldId = field!["id"]!.GetValue<int>()!;
                    string fieldLabel = field!["label"]!.GetValue<string>()!;
                    string cleanFieldLabel = Regex.Replace(
                        fieldLabel.ToLower().Replace(" ", ""),
                        "[^a-zA-Z0-9% ._]",
                        string.Empty
                    );

                    if (
                        fieldId != 3
                        && !string.IsNullOrWhiteSpace(
                            record![$"{fieldId}"]!["value"]!["url"]!.GetValue<string>()!
                        )
                    )

                        foreach (
                            var version in record![$"{fieldId}"]!["value"]!["versions"]!.AsArray()!
                        )
                        {
                            {
                                if (
                                    !string.IsNullOrEmpty(version!["fileName"]!.GetValue<string>()!)
                                )
                                {
                                    string downloadPath;

                                    /* Path using version number as the containing folder. May add as an option later. */
                                    // downloadPath = Path.Combine(
                                    //     workingPath,
                                    //     tableId,
                                    //     recordId.ToString(),
                                    //     fieldId.ToString(),
                                    //     version!["versionNumber"]!.GetValue<int>().ToString()!
                                    // );

                                    if (fieldLabelAsFolderName)
                                    {
                                        downloadPath = Path.Combine(
                                            workingPath,
                                            tableId,
                                            recordId.ToString(),
                                            fieldLabel
                                        );
                                    }
                                    else if (cleanFieldLabelAsFolderName)
                                    {
                                        downloadPath = Path.Combine(
                                            workingPath,
                                            tableId,
                                            recordId.ToString(),
                                            cleanFieldLabel
                                        );
                                    }
                                    else
                                    {
                                        downloadPath = Path.Combine(
                                            workingPath,
                                            tableId,
                                            recordId.ToString(),
                                            fieldId.ToString()
                                        );
                                    }

                                    DirectoryInfo directory = System.IO.Directory.CreateDirectory(
                                        downloadPath
                                    );

                                    string filePath = Path.Combine(
                                        directory.FullName,
                                        "v"
                                            + version!["versionNumber"]!.GetValue<int>()!
                                            + "_"
                                            + version!["fileName"]!.GetValue<string>()!
                                    );

                                    FileInfo existingFile = new FileInfo(filePath);

                                    if (!Path.Exists(filePath) || existingFile.Length <= 0)
                                    {
                                        Console.WriteLine(field);
                                        Console.WriteLine(version);

                                        HttpClient fileClient = new HttpClient();

                                        fileClient.DefaultRequestHeaders.Add(
                                            "Authorization",
                                            "QB-USER-TOKEN " + qbUserToken
                                        );
                                        fileClient.DefaultRequestHeaders.Add(
                                            "QB-Realm-Hostname",
                                            qbRealmHostname
                                        );

                                        Uri downloadUrl = new System.Uri(
                                            fileDownloadBaseUrl
                                                + tableId
                                                + "/"
                                                + recordId
                                                + "/"
                                                + fieldId
                                                + "/"
                                                + version!["versionNumber"]!.GetValue<int>()!
                                        );

                                        var fileDownloadResponse = fileClient
                                            .GetAsync(downloadUrl)
                                            .Result;
                                        var fileBase64 = string.Empty;

                                        if (fileDownloadResponse.IsSuccessStatusCode)
                                        {
                                            fileBase64 = fileDownloadResponse.Content
                                                .ReadAsStringAsync()
                                                .Result;
                                        }
                                        else
                                        {
                                            throw new Exception("Error in getting data");
                                        }

                                        var bytes = Convert.FromBase64String(fileBase64);

                                        fileClient.Dispose();

                                        // FileStream file = File.Create(
                                        //     Path.Combine(
                                        //         directory.FullName,
                                        //         version!["fileName"]!.GetValue<string>()!
                                        //     ),
                                        //     bytes.Length
                                        // );
                                        FileStream file = File.Create(
                                            Path.Combine(
                                                directory.FullName,
                                                "v"
                                                    + version!["versionNumber"]!.GetValue<int>()!
                                                    + "_"
                                                    + version!["fileName"]!.GetValue<string>()!
                                            ),
                                            bytes.Length
                                        );

                                        file.Close();

                                        System.IO.File.WriteAllBytes(file.Name, bytes);

                                        /* For Azure upload later */
                                        // using (Stream stream = new MemoryStream(bytes))
                                        // {
                                        //     // Ensure stream is at the beginning
                                        //     stream.Seek(0, SeekOrigin.Begin);
                                        //     file.Create(stream.Length);
                                        // }
                                    }
                                }
                            }
                        }
                }
            }
        }
    }
}
