"""
Enterprise-grade lifecycle management system for SSSP AI Service.
Manages component startup, health monitoring, dependencies, and graceful shutdown.
"""
from .manager import lifespan
from .base import BaseLifecycleComponent, ComponentState, ComponentPriority
from .registry import ComponentRegistry, register_component
from .health_registry import HealthRegistry, ComponentHealth, HealthStatus

__all__ = [
    'LifespanManager',
    'lifespan',
    'BaseLifecycleComponent',
    'ComponentState',
    'ComponentPriority',
    'ComponentRegistry',
    'register_component',
    'HealthRegistry',
    'ComponentHealth',
    'HealthStatus',
]
