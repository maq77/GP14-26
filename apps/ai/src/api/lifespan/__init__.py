"""
Enterprise-grade lifecycle management system for SSSP AI Service.
Manages component startup, health monitoring, dependencies, and graceful shutdown.
"""

from src.api.lifespan.manager import lifespan
from src.api.lifespan.base import BaseLifecycleComponent, ComponentState, ComponentPriority
from src.api.lifespan.registry import ComponentRegistry, register_component
from src.api.lifespan.health_registry import HealthRegistry, ComponentHealth, HealthStatus


__all__ = [
    "LifespanManager",
    "lifespan",
    "BaseLifecycleComponent",
    "ComponentState",
    "ComponentPriority",
    "ComponentRegistry",
    "register_component",
    "HealthRegistry",
    "ComponentHealth",
    "HealthStatus",
]