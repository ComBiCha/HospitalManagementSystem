using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Net.Http.Headers;
using FellowOakDicom;
using FellowOakDicom.Log;
using FellowOakDicom.Media;
using FellowOakDicom.Imaging;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Jpeg;
using FellowOakDicom.Imaging.Codec;


namespace EpicJwtAssertionGenerator
{
    public class Program
    {

        private static readonly string ProjectRoot =
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"../../../"));

        private static readonly string outputFilePath =
            Path.Combine(ProjectRoot, "DICOM", "Updated_0002.DCM");

        private static readonly string outputImagePath =
            Path.Combine(ProjectRoot, "Image", "0002.jpg");

        private static readonly string PathToDicomTestFile =
            Path.Combine(ProjectRoot, "DICOM", "0002.DCM");
        private static readonly string PathToDicomDirectoryFile =
            Path.Combine(ProjectRoot, "DICOM", "DICOMDIR");

        private static readonly string filePath = PathToDicomTestFile;
        private static readonly string orthancUrl = "http://localhost:8042/instances";
        private static readonly string username = "orthanc";
        private static readonly string password = "orthanc";
        private static readonly string patientURL = "http://localhost:8042/patients";
        private static readonly string studyURL = "http://localhost:8042/studies";
        private static readonly string seriesURL = "http://localhost:8042/series";
        private static readonly string instancesURL = "http://localhost:8042/instances";

        public static void GetAllTagsFromDicom()
        {
            try
            {
                LogToDebugConsole($"Attempting to extract information from DICOM file:{PathToDicomTestFile}...");

                var file = DicomFile.Open(PathToDicomTestFile);

                foreach (var tag in file.Dataset)
                {
                    LogToDebugConsole($" {tag} '{file.Dataset.GetValueOrDefault(tag.Tag, 0, "")}'");
                }

                LogToDebugConsole($"Extract operation from DICOM file successful");
            }
            catch (Exception e)
            {
                LogToDebugConsole($"Error occured during DICOM file dump operation -> {e.StackTrace}");
            }
        }

        public static void DicomDirectoryDump()
        {
            LogToDebugConsole("Performing Dicom directory dump:");

            try
            {
                var dicomDirectory = DicomDirectory.Open(PathToDicomDirectoryFile);

                LogToDebugConsole(dicomDirectory.WriteToString());

                LogToDebugConsole("Dicom directory dump operation was successful");
            }
            catch (Exception ex)
            {
                LogToDebugConsole($"Error occured during Dicom directory dump. Error:{ex.Message}");
                }
        }

        public static void PrintDicomPatientInfo(string dicomFilePath)
        {
            var file = DicomFile.Open(dicomFilePath);
            var dataset = file.Dataset;

            var patientName = dataset.GetSingleValueOrDefault(DicomTag.PatientName, "Unknown");
            var patientId = dataset.GetSingleValueOrDefault(DicomTag.PatientID, "Unknown");
            var birthDate = dataset.GetSingleValueOrDefault(DicomTag.PatientBirthDate, "Unknown");
            var gender = dataset.GetSingleValueOrDefault(DicomTag.PatientSex, "Unknown");

            Console.WriteLine($"Patient Name: {patientName}");
            Console.WriteLine($"Patient ID: {patientId}");
            Console.WriteLine($"Birth Date: {birthDate}");
            Console.WriteLine($"Gender: {gender}");
        }
        public static void UpdateDicomPatientName(string dicomFilePath, string newName, string outputFilePath)
        {
            var file = DicomFile.Open(dicomFilePath);
            var dataset = file.Dataset;

            dataset.AddOrUpdate(DicomTag.PatientName, newName);

            file.Save(outputFilePath);
            Console.WriteLine($"Updated Patient Name to: {newName} and saved to {outputFilePath}");
        }

        public static async Task UploadDicomToServer()
        {
            using (var client = new HttpClient())
            {
                var byteArray = Encoding.ASCII.GetBytes($"{username}:{password}");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                using (var content = new ByteArrayContent(File.ReadAllBytes(filePath)))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/dicom");

                    HttpResponseMessage response = await client.PostAsync(orthancUrl, content);
                    string result = await response.Content.ReadAsStringAsync();

                    Console.WriteLine("Upload status: " + response.StatusCode);
                    Console.WriteLine("Response: " + result);
                }
            }
        }
        public static async Task GetAllPatients()
        {
            using var client = CreateHttpClient();
            var response = await client.GetAsync(patientURL);
            string result = await response.Content.ReadAsStringAsync();
            Console.WriteLine("All patients: " + result);
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            var byteArray = Encoding.ASCII.GetBytes("orthanc:orthanc");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            return client;
        }
        public static async Task GetPatientDetails(string patientId)
        {
            using var client = CreateHttpClient();
            var response = await client.GetAsync(patientURL + $"/{patientId}");
            string result = await response.Content.ReadAsStringAsync();
            Console.WriteLine("Patient details: " + result);
        }
        public static async Task GetStudyDetails(string studyId)
        {
            using var client = CreateHttpClient();
            var response = await client.GetAsync(studyURL + $"/{studyId}");
            string result = await response.Content.ReadAsStringAsync();
            Console.WriteLine("Study details: " + result);
        }
        public static async Task GetSeriesDetails(string seriesId)
        {
            using var client = CreateHttpClient();
            var response = await client.GetAsync(seriesURL + $"/{seriesId}");
            string result = await response.Content.ReadAsStringAsync();
            Console.WriteLine("Series details: " + result);
        }
        public static async Task GetInstanceDetails(string instanceId)
        {
            using var client = CreateHttpClient();
            var response = await client.GetAsync(instancesURL + $"/{instanceId}");
            string result = await response.Content.ReadAsStringAsync();
            Console.WriteLine("Instance details: " + result);
        }
        public static async Task DownloadInstanceFile(string instanceId, string savePath)
        {
            using var client = CreateHttpClient();
            var response = await client.GetAsync(instancesURL + $"/{instanceId}/file");
            var bytes = await response.Content.ReadAsByteArrayAsync();
            File.WriteAllBytes(savePath, bytes);
            Console.WriteLine($"DICOM file saved to {savePath}");
        }
        public static async Task DownloadInstanceAsJpeg(string instanceId, string savePath)
        {
            using var client = CreateHttpClient();
            var response = await client.GetAsync(instancesURL + $"/{instanceId}/preview");
            var bytes = await response.Content.ReadAsByteArrayAsync();
            File.WriteAllBytes(savePath, bytes);
            Console.WriteLine($"JPEG saved to {savePath}");
        }









        // public static void Main(string[] args)
        // {

        //     // PrintDicomPatientInfo(PathToDicomTestFile);
        //     // UpdateDicomPatientName(PathToDicomTestFile, "New^PatientName", outputFilePath);
        //     // PrintDicomPatientInfo(outputFilePath);

        // }

        static async Task Main(string[] args)
        {
            // await UploadDicomToServer();
            await GetAllPatients();
            await GetPatientDetails("1e6ca9a6-71d9b622-24e72d15-16251ee8-3735baf5");
            await GetStudyDetails("aa938400-09e9f0df-8ba95f68-e21f98dd-0c6e0cf0");
            await GetSeriesDetails("62cc4f41-9b1679c0-6ad19a2e-71fccb6f-07a3d16d");
            await GetInstanceDetails("b7fb1fb4-142e5a15-333ae197-298b1626-11f8833b");

            await DownloadInstanceFile("b7fb1fb4-142e5a15-333ae197-298b1626-11f8833b", "downloaded.dcm");
            await DownloadInstanceAsJpeg("b7fb1fb4-142e5a15-333ae197-298b1626-11f8833b", "preview.jpg");
        }   

        private static void LogToDebugConsole(string informationToLog)
        {
            Console.WriteLine(informationToLog);
        }

        //API ORTHANC
        //==========================================================================================================================
        //DICOM NETWORKING

        // private const string OrthancAET = "ORTHANC";
        // private const string LocalAET = "MYCLIENT";
        // private const string OrthancIP = "127.0.0.1";
        // private const int OrthancPort = 4242; 

        // public static async Task Main(string[] args)
        // {
        //     var port = args != null && args.Length > 0 && int.TryParse(args[0], out int tmp) ? tmp : 11112;
        //     Console.WriteLine($"Starting C-Store SCP server on port {port}");

        //     new DicomSetupBuilder()
        //         .RegisterServices(s => s.AddFellowOakDicom())
        //     .Build();

        //     using (var server = DicomServerFactory.Create<StoreScp>(port))
        //     {

        //         await SendCEcho();

        //         await SendCStore("0003.dcm");

        //         await SendCFind();

        //         await SendCMove("1.3.12.2.1107.5.4.3.123456789012345.19950922.121803.6");
        //         // end process
        //         Console.WriteLine("Press <return> to end...");
        //         Console.ReadLine();
        //     }




        // }

        // private static async Task SendCEcho()
        // {
        //     var client = DicomClientFactory.Create(OrthancIP, OrthancPort, false, LocalAET, OrthancAET);
        //     var echo = new DicomCEchoRequest();
        //     echo.OnResponseReceived += (req, resp) =>
        //     {
        //         Console.WriteLine("C-ECHO response: " + resp.Status);
        //     };
        //     await client.AddRequestAsync(echo);
        //     await client.SendAsync();
        // }

        // private static async Task SendCStore(string dicomPath)
        // {
        //     var dicomFile = DicomFile.Open(dicomPath);
        //     var client = DicomClientFactory.Create(OrthancIP, OrthancPort, false, LocalAET, OrthancAET);

        //     var request = new DicomCStoreRequest(dicomFile);
        //     request.OnResponseReceived += (req, resp) =>
        //     {
        //         Console.WriteLine("C-STORE response: " + resp.Status);
        //     };

        //     await client.AddRequestAsync(request);
        //     await client.SendAsync();
        // }

        // private static async Task SendCFind()
        // {
        //     var client = DicomClientFactory.Create(OrthancIP, OrthancPort, false, LocalAET, OrthancAET);

        //     var request = new DicomCFindRequest(DicomQueryRetrieveLevel.Study)
        //     {
        //         Dataset =
        //         {
        //             { DicomTag.PatientID, "556342B" }, 
        //             { DicomTag.StudyDate, "" },      
        //             { DicomTag.StudyInstanceUID, ""}
        //         }
        //     };

        //     request.OnResponseReceived += (req, resp) =>
        //     {
        //         Console.WriteLine("C-FIND response: " + resp.Dataset?.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, ""));
        //     };

        //     await client.AddRequestAsync(request);
        //     await client.SendAsync();
        // }
        
        // private static async Task SendCMove(string studyUID)
        // {
        //     var client = DicomClientFactory.Create(OrthancIP, OrthancPort, false, LocalAET, OrthancAET);

        //     var request = new DicomCMoveRequest(LocalAET, studyUID);
        //     request.OnResponseReceived += (req, resp) =>
        //     {
        //         Console.WriteLine("C-MOVE response: " + resp.Status);
        //     };

        //     await client.AddRequestAsync(request);
        //     await client.SendAsync();
        // }

    }
}
