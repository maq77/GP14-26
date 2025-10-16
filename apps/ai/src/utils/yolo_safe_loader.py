# apps/ai/src/models/object/safe_globals.py
import inspect
import torch
import torch.nn as nn
import ultralytics.nn.modules as yolo_modules

def register_ultralytics_safe_globals():
    """
    Register YOLO/Ultralytics classes as safe globals for PyTorch >= 2.6,
    and create legacy aliases on `ultralytics.nn.modules` so pickle can
    resolve names like `ultralytics.nn.modules.Conv`.
    Returns the list of classes actually registered.
    """
    safe_classes = []

    for _, obj in inspect.getmembers(yolo_modules, inspect.isclass):
        safe_classes.append(obj)

    for _, submod in inspect.getmembers(yolo_modules, inspect.ismodule):
        for name, obj in inspect.getmembers(submod, inspect.isclass):
            safe_classes.append(obj)
            if getattr(yolo_modules, name, None) is None:
                setattr(yolo_modules, name, obj)

    safe_classes.extend([
        nn.Sequential, nn.ModuleList, nn.Conv2d, nn.BatchNorm2d,
        nn.ReLU, nn.SiLU, nn.Upsample, nn.Identity
    ])

    safe_classes = list({cls for cls in safe_classes if cls is not None and isinstance(cls, type)})
    torch.serialization.add_safe_globals(safe_classes)

    print(f"[SafeGlobals] Registered {len(safe_classes)} YOLO/PyTorch classes for secure model loading.")
    return safe_classes
