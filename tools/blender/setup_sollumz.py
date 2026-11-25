"""Enable Sollumz in the project-local Blender profile and install its Python deps.
Usage: blender -b --online-mode -P setup_sollumz.py -- [--with-pymateria]"""
import sys, importlib, addon_utils, bpy
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
mod = "bl_ext.user_default.sollumz"
addon_utils.enable(mod, default_set=True, persistent=True)
deps = importlib.import_module(mod + ".dependencies")
opt = {"pymateria"} if "--with-pymateria" in args else set()
ok = deps.install_dependencies(online_access_override=True, optional_dependencies_to_install=opt)
print("DEPS_INSTALL_OK", ok)
deps.mount_dependencies()
print("SZIO", deps.IS_SZIO_AVAILABLE, "NATIVE", deps.IS_SZIO_NATIVE_AVAILABLE)
bpy.ops.wm.save_userpref()
