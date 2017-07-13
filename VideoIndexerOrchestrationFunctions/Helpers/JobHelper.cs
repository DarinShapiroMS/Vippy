// =============================================================================================================
// Adapted code from https://github.com/johndeu/media-services-dotnet-functions-integration/tree/master/shared
//  Special thanks to John Deutscher (https://github.com/johndeu) and Xavier Pouyat (https://github.com/xpouyat)
// 
// =============================================================================================================

using Microsoft.WindowsAzure.MediaServices.Client;
using System;
using System.IO;

namespace OrchestrationFunctions
{
    public class JobHelper
    {
        public static int AddTask(IJob job, IAsset sourceAsset, string value, string processor, string presetfilename, string stringtoreplace, ref int taskindex, int priority = 10)
        {
            if (value != null)
            {
                // Get a media processor reference, and pass to it the name of the 
                // processor to use for the specific task.
                IMediaProcessor mediaProcessor = MediaServicesHelper.GetLatestMediaProcessorByName(processor);

                string homePath = Environment.GetEnvironmentVariable("HOME", EnvironmentVariableTarget.Process);
                string presetPath;

                if (homePath == String.Empty)
                {
                    presetPath = @"../presets/" + presetfilename;
                }
                else
                {
                    //TODO: update pathing to make dynamic or config based
                    presetPath = Path.Combine(homePath, @"site\repository\media-functions-for-logic-app\presets\" + presetfilename);
                }

                string Configuration = File.ReadAllText(presetPath).Replace(stringtoreplace, value);

                // Create a task with the encoding details, using a string preset.
                var task = job.Tasks.AddNew(processor + " task",
                   mediaProcessor,
                   Configuration,
                   TaskOptions.None);

                task.Priority = priority;

                // Specify the input asset to be indexed.
                task.InputAssets.Add(sourceAsset);

                // Add an output asset to contain the results of the job.
                task.OutputAssets.AddNew(sourceAsset.Name + " " + processor + " Output", AssetCreationOptions.None);

                return taskindex++;
            }
            else
            {
                return -1;
            }
        }

        public static string ReturnId(IJob job, int index)
        {
            return index > -1 ? job.OutputMediaAssets[index].Id : null;
        }

        public static string ReturnTaskId(IJob job, int index)
        {
            return index > -1 ? job.Tasks[index].Id : null;
        }

    }
}
