# Changelog

## [0.2.0] - 2026-11-24
- Presets (`VehiclePreset` ScriptableObject): Default, Sports, Truck, OffRoad, plus your own via
  Create > Vehicle Physics > Vehicle Preset. Selectable in the builder; "apply preset" in the
  VehicleController Inspector.
- Builder handles downloaded models better: centers wheel pivots automatically, makes non-convex
  mesh colliders convex, finds more wheel naming styles, warns when the model doesn't face +Z.
- Three sample models in `Vehicles/` (SportsCar, Truck, Buggy) with their Blender source in `Art~`.
- README: "Using models from outside" checklist and troubleshooting.
- 6 new PlayMode tests that build and drive the sample models (13 total).
- Breaking: `VehicleSetup.Options` now takes a `preset` instead of separate mass/suspension fields.

## [0.1.0] - 2026-11-24
- Initial version: VehicleController, VehicleWheel (raycast suspension with bump/rebound damping
  and bump stops, friction-circle tire model), Ackermann steering, anti-roll bars, handbrake,
  PlayerVehicleInput, VehicleCamera, VehicleHud.
- Test car and procedural test track; Tools > Vehicle Physics menus and Builder window.
- 7 PlayMode tests.
