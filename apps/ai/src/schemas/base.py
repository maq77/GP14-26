# apps/ai/src/schemas/base.py
from pydantic import BaseModel as _BaseModel, ConfigDict

class BaseModel(_BaseModel):
    model_config = ConfigDict(protected_namespaces=())
