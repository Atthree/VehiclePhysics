using UnityEditor;
using UnityEngine;

namespace Atthree.Vehicles.Editor
{
    /// <summary>Tools > Vehicle Physics menüsü.</summary>
    public static class VehicleMenus
    {
        [MenuItem("Tools/Vehicle Physics/Create Test Scene (Track + Car)", priority = 0)]
        static void CreateTestScene()
        {
            var track = TestTrack.Create(Vector3.zero);
            Undo.RegisterCreatedObjectUndo(track, "Create Test Track");
            var car = CreateCarAt(new Vector3(0f, 0.2f, 5f));
            Selection.activeGameObject = car.gameObject;
        }

        [MenuItem("Tools/Vehicle Physics/Create Test Car", priority = 1)]
        static void CreateTestCar()
        {
            // Sahne görünümünün baktığı yere koy.
            var view = SceneView.lastActiveSceneView;
            Vector3 position = view != null ? view.pivot : Vector3.zero;
            var car = CreateCarAt(position + Vector3.up * 0.2f);
            Selection.activeGameObject = car.gameObject;
        }

        [MenuItem("Tools/Vehicle Physics/Create Test Track", priority = 2)]
        static void CreateTestTrack()
        {
            var track = TestTrack.Create(Vector3.zero);
            Undo.RegisterCreatedObjectUndo(track, "Create Test Track");
            Selection.activeGameObject = track;
        }

        static VehicleController CreateCarAt(Vector3 position)
        {
            var car = VehiclePrimitives.CreateTestCar(position, Quaternion.identity);
            Undo.RegisterCreatedObjectUndo(car.gameObject, "Create Test Car");
            var cam = VehicleSetup.SetupCamera(car);
            Undo.RegisterCompleteObjectUndo(cam.gameObject, "Setup Camera");
            return car;
        }
    }
}
