using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grabber
{
    public static class PersonGroupFactory
    {       
        internal static async void CreatePersonGroup(string groupId, string Name, string imageFilePath, IFaceServiceClient faceServiceClient)
        {
            // Create an empty person group
            string personGroupId = groupId;
            await faceServiceClient.CreatePersonGroupAsync(personGroupId, "Me and I and U");

            // Define Rex
            CreatePersonResult rex = await faceServiceClient.CreatePersonAsync(
                // Id of the person group that the person belonged to
                personGroupId,
                // Name of the person
                Name
            );

            using (Stream s = File.OpenRead(imageFilePath))
            {
                // Detect faces in the image and add to Anna
                await faceServiceClient.AddPersonFaceAsync(
                    personGroupId, rex.PersonId, s);
            }

            //train personGroup
            await faceServiceClient.TrainPersonGroupAsync(personGroupId);

            //TrainingStatus trainingStatus = null;
            //while (true)
            //{
            //    trainingStatus = await faceServiceClient.GetPersonGroupTrainingStatusAsync(personGroupId);

            //    if (trainingStatus.Status == Status.Succeeded)
            //    {
            //        return true;
            //    }

            //    await Task.Delay(500);
            //}

        }
    }
}
