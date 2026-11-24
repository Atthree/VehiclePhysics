# Blender script: VehiclePhysics paketinin örnek araçlarını üretir ve FBX olarak dışa aktarır.
#
#   blender -b --factory-startup -P make_vehicles.py -- <çıktı_klasörü>
#
# Üç araç, "dışarıdan alınmış model"lerde sık görülen durumları bilerek içerir:
#   SportsCar : temiz model. Tekerlek pivotları merkezde, isimler Wheel_FL ...
#   Truck     : tekerlek pivotları tekerleğin merkezinde DEĞİL (model orijininde).
#               Builder bunu otomatik düzeltmeli.
#   Buggy     : araç Unity'de +X yönüne bakıyor (yanlış eksen), isimler wheel.fl ...
#               README'deki "boş obje içine koyup döndür" adımıyla düzeltilir.

import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

out_dir = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.dirname(__file__)
bpy.ops.wm.read_factory_settings(use_empty=True)
UP = Vector((0, 0, 1))


def material(name, color):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = color
    if m.node_tree is None and hasattr(m, "use_nodes"):
        m.use_nodes = True
    if m.node_tree:
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = color
            bsdf.inputs["Roughness"].default_value = 0.5
    return m


TIRE = material("Tire", (0.06, 0.06, 0.07, 1))
RIM = material("Rim", (0.8, 0.8, 0.82, 1))
GLASS = material("Glass", (0.12, 0.16, 0.22, 1))
DARK = material("Trim", (0.15, 0.15, 0.16, 1))


class Car:
    """Araç uzayı: r = sağ, f = ileri, u = yukarı (metre). Blender eksenlerine çevirir."""

    def __init__(self, name, forward_blender):
        self.F = Vector(forward_blender).normalized()
        self.R = self.F.cross(UP).normalized()
        self.root = bpy.data.objects.new(name, None)
        bpy.context.scene.collection.objects.link(self.root)
        self.name = name

    def to_blender(self, r, f, u):
        return self.R * r + self.F * f + UP * u

    def box(self, name, center, size, mat):
        """center/size: (r, f, u). Kutu kendi merkezinde pivotlu."""
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        half = Vector((abs(self.R.x) * size[0] + abs(self.F.x) * size[1],
                       abs(self.R.y) * size[0] + abs(self.F.y) * size[1],
                       size[2]))
        for v in bm.verts:
            v.co = Vector((v.co.x * half.x, v.co.y * half.y, v.co.z * half.z))
        return self._object(name, bm, [mat], self.to_blender(*center))

    def wheel(self, name, center, radius, width, pivot_at_center=True):
        """Lastik + jant + jant üzerinde döndüğü görünsün diye bir kol."""
        bm = bmesh.new()
        axle = Matrix.Rotation(math.radians(90), 4, 'Y')  # silindir Z -> X ekseni
        tire = bmesh.ops.create_cone(bm, cap_ends=True, segments=28, radius1=radius, radius2=radius, depth=width, matrix=axle)
        rim = bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=radius * 0.62, radius2=radius * 0.62, depth=width * 1.08, matrix=axle)
        spoke = bmesh.ops.create_cube(bm, size=1.0)
        for v in spoke["verts"]:
            v.co = Vector((v.co.x * width * 1.16, v.co.y * radius * 0.18, v.co.z * radius * 1.1))
        rim_faces = {f for v in rim["verts"] + spoke["verts"] for f in v.link_faces}
        for f in bm.faces:
            f.material_index = 1 if f in rim_faces else 0
            f.smooth = f not in rim_faces

        # Tekerlek aksı aracın sağ eksenine hizalansın.
        align = Matrix.Identity(4)
        align.col[0].xyz = self.R
        align.col[1].xyz = self.F
        align.col[2].xyz = UP
        bmesh.ops.transform(bm, matrix=align, verts=bm.verts)

        location = self.to_blender(*center)
        if pivot_at_center:
            return self._object(name, bm, [TIRE, RIM], location)
        # Pivot araç orijininde: köşe noktaları tekerleğin yerinde, objenin konumu (0,0,0).
        bmesh.ops.translate(bm, vec=location, verts=bm.verts)
        return self._object(name, bm, [TIRE, RIM], Vector((0, 0, 0)))

    def _object(self, name, bm, mats, location):
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        for m in mats:
            mesh.materials.append(m)
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj.location = location
        obj.parent = self.root
        return obj

    def export(self, path):
        bpy.ops.object.select_all(action="DESELECT")
        self.root.select_set(True)
        for c in self.root.children:
            c.select_set(True)
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={"EMPTY", "MESH"},
            apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
            bake_space_transform=True, bake_anim=False, mesh_smooth_type="FACE")
        print("EXPORTED", path)


# Blender'da -Y yönü, Unity'de +Z (ileri) olur.
UNITY_FORWARD_Z = (0, -1, 0)
UNITY_FORWARD_X = (-1, 0, 0)   # Unity'de +X: "yanlış eksenli" model

# ------------------------------------------------------------------ Spor araba
car = Car("SportsCar", UNITY_FORWARD_Z)
body = material("SportsCar_Paint", (0.1, 0.35, 0.85, 1))
car.box("Body", (0, 0, 0.55), (1.9, 4.3, 0.45), body)
car.box("Hood", (0, 1.45, 0.8), (1.8, 1.3, 0.08), body)
car.box("Cabin", (0, -0.25, 0.98), (1.5, 1.9, 0.42), GLASS)
car.box("Spoiler", (0, -2.05, 1.0), (1.7, 0.25, 0.06), DARK)
r, hw = 0.33, 0.26
for tag, (x, y) in {"FL": (-0.82, 1.35), "FR": (0.82, 1.35), "RL": (-0.82, -1.3), "RR": (0.82, -1.3)}.items():
    car.wheel(f"Wheel_{tag}", (x, y, r), r, hw)
car.export(os.path.join(out_dir, "SportsCar.fbx"))

# ------------------------------------------------------------------ Kamyon (pivotlar yanlış)
truck = Car("Truck", UNITY_FORWARD_Z)
paint = material("Truck_Paint", (0.95, 0.75, 0.15, 1))
truck.box("Chassis", (0, 0, 0.95), (2.2, 6.6, 0.35), DARK)
truck.box("Cab", (0, 2.2, 1.95), (2.3, 2.0, 1.7), paint)
truck.box("Windshield", (0, 3.15, 2.3), (2.1, 0.1, 0.8), GLASS)
truck.box("Cargo", (0, -1.0, 2.05), (2.4, 4.3, 1.9), material("Truck_Cargo", (0.85, 0.85, 0.82, 1)))
r, hw = 0.5, 0.42
for tag, name, (x, y) in [("FL", "FrontLeftWheel", (-1.0, 2.3)), ("FR", "FrontRightWheel", (1.0, 2.3)),
                          ("RL", "RearLeftWheel", (-1.0, -1.9)), ("RR", "RearRightWheel", (1.0, -1.9))]:
    truck.wheel(name, (x, y, r), r, hw, pivot_at_center=False)
truck.export(os.path.join(out_dir, "Truck.fbx"))

# ------------------------------------------------------------------ Buggy (yanlış yöne bakıyor)
buggy = Car("Buggy", UNITY_FORWARD_X)
paint = material("Buggy_Paint", (0.95, 0.42, 0.1, 1))
buggy.box("Frame", (0, 0, 0.75), (1.5, 2.9, 0.25), paint)
buggy.box("Seat", (0, -0.25, 1.05), (0.9, 0.7, 0.45), DARK)
buggy.box("Engine", (0, -1.15, 1.0), (1.0, 0.6, 0.45), DARK)
buggy.box("RollBar", (0, -0.6, 1.55), (1.3, 0.12, 0.12), DARK)
buggy.box("RollBarL", (-0.6, -0.6, 1.2), (0.12, 0.12, 0.7), DARK)
buggy.box("RollBarR", (0.6, -0.6, 1.2), (0.12, 0.12, 0.7), DARK)
buggy.box("Nose", (0, 1.3, 0.85), (1.2, 0.5, 0.2), paint)
r, hw = 0.46, 0.36
for tag, (x, y) in {"fl": (-0.95, 1.15), "fr": (0.95, 1.15), "rl": (-0.95, -1.15), "rr": (0.95, -1.15)}.items():
    buggy.wheel(f"wheel.{tag}", (x, y, r), r, hw)
buggy.export(os.path.join(out_dir, "Buggy.fbx"))

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_dir, "Vehicles.blend"))
