"""
Enterprise-grade lifecycle management system for SSSP AI Service.
Manages component startup, health monitoring, dependencies, and graceful shutdown.
"""
from api.lifespan.manager import lifespan
from api.lifespan.base import BaseLifecycleComponent, ComponentState, ComponentPriority
from api.lifespan.registry import ComponentRegistry, register_component
from api.lifespan.health_registry import HealthRegistry, ComponentHealth, HealthStatus

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
