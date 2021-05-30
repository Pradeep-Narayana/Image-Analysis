using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;


namespace image_analysis
{
    class Program
    {

        private static ComputerVisionClient cvClient;
        static async Task Main(string[] args)
        {
            try
            {
                // Get config settings from AppSettings
                IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
                IConfigurationRoot configuration = builder.Build();
                string cogSvcEndpoint = configuration["CognitiveServicesEndpoint"];
                string cogSvcKey = configuration["CognitiveServiceKey"];

                // Get image
                string imageFile = "images/street.jpg";
                if (args.Length > 0)
                {
                    imageFile = args[0];
                }

                // Authenticate Computer Vision client
                ApiKeyServiceClientCredentials credentials = new ApiKeyServiceClientCredentials(cogSvcKey);
                cvClient = new ComputerVisionClient(credentials)
                {
                    Endpoint = cogSvcEndpoint
                };

                // Analyze image
                await AnalyzeImage(imageFile);

                // Get thumbnail
                await GetThumbnail(imageFile);


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static async Task AnalyzeImage(string imageFile)
        {
            var directory = Directory.GetParent(@"../../../").FullName;
            StreamWriter streamWriter = new StreamWriter(directory + "//output//output.txt");

            streamWriter.WriteLine($"Analyzing {imageFile}");

            // Specify features to be retrieved
            List<VisualFeatureTypes?> features = new List<VisualFeatureTypes?>()
            {
                VisualFeatureTypes.Description,
                VisualFeatureTypes.Tags,
                VisualFeatureTypes.Categories,
                VisualFeatureTypes.Brands,
                VisualFeatureTypes.Objects,
                VisualFeatureTypes.Adult
            };

            // Get image analysis
            using (var imageData = File.OpenRead("../../../" + imageFile))
            {
                var analysis = await cvClient.AnalyzeImageInStreamAsync(imageData, features);

                // get image captions
                foreach (var caption in analysis.Description.Captions)
                {
                    streamWriter.WriteLine($"Description: {caption.Text} (confidence: {caption.Confidence.ToString("P")})");
                }

                // Get image tags
                if (analysis.Tags.Count > 0)
                {
                    streamWriter.WriteLine("Tags:");
                    foreach (var tag in analysis.Tags)
                    {
                        streamWriter.WriteLine($" -{tag.Name} (confidence: {tag.Confidence.ToString("P")})");
                    }
                }

                // Get image categories
                List<LandmarksModel> landmarks = new List<LandmarksModel> { };
                List<CelebritiesModel> celebrities = new List<CelebritiesModel> { };
                streamWriter.WriteLine("Categories:");
                foreach (var category in analysis.Categories)
                {
                    // Print the category
                    streamWriter.WriteLine($" -{category.Name} (confidence: {category.Score.ToString("P")})");

                    // Get landmarks in this category
                    if (category.Detail?.Landmarks != null)
                    {
                        foreach (LandmarksModel landmark in category.Detail.Landmarks)
                        {
                            if (!landmarks.Any(item => item.Name == landmark.Name))
                            {
                                landmarks.Add(landmark);
                            }
                        }
                    }

                    // Get celebrities in this category
                    if (category.Detail?.Celebrities != null)
                    {
                        foreach (CelebritiesModel celebrity in category.Detail.Celebrities)
                        {
                            if (!celebrities.Any(item => item.Name == celebrity.Name))
                            {
                                celebrities.Add(celebrity);
                            }
                        }
                    }
                }

                // If there were landmarks, list them
                if (landmarks.Count > 0)
                {
                    streamWriter.WriteLine("Landmarks:");
                    foreach (LandmarksModel landmark in landmarks)
                    {
                        streamWriter.WriteLine($" -{landmark.Name} (confidence: {landmark.Confidence.ToString("P")})");
                    }
                }

                // If there were celebrities, list them
                if (celebrities.Count > 0)
                {
                    streamWriter.WriteLine("Celebrities:");
                    foreach (CelebritiesModel celebrity in celebrities)
                    {
                        streamWriter.WriteLine($" -{celebrity.Name} (confidence: {celebrity.Confidence.ToString("P")})");
                    }
                }

                // Get brands in the image
                if (analysis.Brands.Count > 0)
                {
                    streamWriter.WriteLine("Brands:");
                    foreach (var brand in analysis.Brands)
                    {
                        streamWriter.WriteLine($" -{brand.Name} (confidence: {brand.Confidence.ToString("P")})");
                    }
                }

                // Get objects in the image
                if (analysis.Objects.Count > 0)
                {
                    streamWriter.WriteLine("Objects in image:");

                    // Prepare image for drawing
                    Image image = Image.FromFile("../../../" + imageFile);
                    Graphics graphics = Graphics.FromImage(image);
                    Pen pen = new Pen(Color.Cyan, 3);
                    Font font = new Font("Arial", 16);
                    SolidBrush brush = new SolidBrush(Color.Black);

                    foreach (var detectedObject in analysis.Objects)
                    {
                        // Print object name
                        streamWriter.WriteLine($" -{detectedObject.ObjectProperty} (confidence: {detectedObject.Confidence.ToString("P")})");

                        // Draw object bounding box
                        var r = detectedObject.Rectangle;
                        Rectangle rect = new Rectangle(r.X, r.Y, r.W, r.H);
                        graphics.DrawRectangle(pen, rect);
                        graphics.DrawString(detectedObject.ObjectProperty, font, brush, r.X, r.Y);

                    }
                    // Save annotated image
                    String output_file = "objects.jpg";
                    image.Save("../../../" + "//output//" + output_file);
                    streamWriter.WriteLine("  Results saved in " + output_file);
                }

                // Get moderation ratings
                string ratings = $"Ratings:\n -Adult: {analysis.Adult.IsAdultContent}\n -Racy: {analysis.Adult.IsRacyContent}\n -Gore: {analysis.Adult.IsGoryContent}";
                streamWriter.WriteLine(ratings);
                streamWriter.Close();
            }
        }

        static async Task GetThumbnail(string imageFile)
        {
            Console.WriteLine("Generating thumbnail");

            // Generate a thumbnail
            using (var imageData = File.OpenRead("../../../" + imageFile))
            {
                // Get thumbnail data
                var thumbnailStream = await cvClient.GenerateThumbnailInStreamAsync(100, 100, imageData, true);

                // Save thumbnail image
                string thumbnailFileName = "thumbnail.png";
                using (Stream thumbnailFile = File.Create("../../../" + "//output//" + thumbnailFileName))
                {
                    thumbnailStream.CopyTo(thumbnailFile);
                }

                Console.WriteLine($"Thumbnail saved in {thumbnailFileName}");
            }
        }


    }
}
