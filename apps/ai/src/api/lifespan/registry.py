"""Component registry for automatic discovery and registration."""
from typing import List, Type, Dict, Set, Optional
import structlog
from src.api.lifespan.base import BaseLifecycleComponent

logger = structlog.get_logger(__name__)


class ComponentRegistry:
    """
    Singleton registry for all lifecycle components.
    
    Features:
    - Decorator-based registration
    - Dependency validation
    - Circular dependency detection
    - Priority-based ordering
    """
    
    _instance: Optional['ComponentRegistry'] = None
    _components: List[Type[BaseLifecycleComponent]] = []
    _component_map: Dict[str, Type[BaseLifecycleComponent]] = {}
    
    def __new__(cls):
        if cls._instance is None:
            cls._instance = super().__new__(cls)
        return cls._instance
    
    @classmethod
    def register(cls, component_class: Type[BaseLifecycleComponent]) -> Type[BaseLifecycleComponent]:
        """
        Register a component class.
        
        Args:
            component_class: The component class to register
            
        Returns:
            The same component class (for use as decorator)
            
        Raises:
            TypeError: If component doesn't inherit from BaseLifecycleComponent
            ValueError: If component name is invalid
        """
        # Validation
        if not issubclass(component_class, BaseLifecycleComponent):
            raise TypeError(
                f"{component_class.__name__} must inherit from BaseLifecycleComponent"
            )
        
        if not hasattr(component_class, 'name') or component_class.name == "UnnamedComponent":
            raise ValueError(
                f"Component {component_class.__name__} must define a 'name' attribute"
            )
        
        # Check for duplicates
        if component_class.name in cls._component_map:
            existing = cls._component_map[component_class.name]
            logger.warning(
                "duplicate_component_registration",
                name=component_class.name,
                existing_class=existing.__name__,
                new_class=component_class.__name__,
                action="skipping"
            )
            return component_class
        
        # Register
        cls._components.append(component_class)
        cls._component_map[component_class.name] = component_class
        
        logger.debug(
            "component_registered",
            name=component_class.name,
            class_name=component_class.__name__,
            priority=component_class.priority.value,
            depends_on=component_class.depends_on
        )
        
        return component_class
    
    @classmethod
    def get_all(cls) -> List[Type[BaseLifecycleComponent]]:
        """Get all registered component classes."""
        return cls._components.copy()
    
    @classmethod
    def get_by_name(cls, name: str) -> Optional[Type[BaseLifecycleComponent]]:
        """Get a component class by name."""
        return cls._component_map.get(name)
    
    @classmethod
    def get_sorted_by_priority(cls) -> List[Type[BaseLifecycleComponent]]:
        """
        Get components sorted by priority for startup order.
        Lower priority values start first.
        """
        return sorted(cls._components, key=lambda c: (c.priority.value, c.name))
    
    @classmethod
    def validate_dependencies(cls) -> bool:
        """
        Validate that all component dependencies are registered.
        
        Returns:
            True if all dependencies are valid
            
        Raises:
            ValueError: If any dependency is missing or circular
        """
        all_names = set(cls._component_map.keys())
        
        # Check for missing dependencies
        for component_class in cls._components:
            for dep in component_class.depends_on:
                if dep not in all_names:
                    raise ValueError(
                        f"Component '{component_class.name}' depends on '{dep}' "
                        f"which is not registered. Available: {sorted(all_names)}"
                    )
        
        # Check for circular dependencies
        cls._check_circular_dependencies()
        
        logger.info(
            "dependency_validation_passed",
            total_components=len(cls._components),
            component_names=sorted(all_names)
        )
        
        return True
    
    @classmethod
    def _check_circular_dependencies(cls):
        """
        Detect circular dependencies using depth-first search.
        
        Raises:
            ValueError: If circular dependency detected
        """
        def visit(node: str, visited: Set[str], rec_stack: Set[str], path: List[str]) -> bool:
            visited.add(node)
            rec_stack.add(node)
            path.append(node)
            
            component_class = cls._component_map.get(node)
            if component_class:
                for dep in component_class.depends_on:
                    if dep not in visited:
                        if visit(dep, visited, rec_stack, path):
                            return True
                    elif dep in rec_stack:
                        # Found cycle
                        cycle_start = path.index(dep)
                        cycle = " -> ".join(path[cycle_start:] + [dep])
                        raise ValueError(f"Circular dependency detected: {cycle}")
            
            path.pop()
            rec_stack.remove(node)
            return False
        
        visited = set()
        for name in cls._component_map.keys():
            if name not in visited:
                visit(name, visited, set(), [])
    
    @classmethod
    def clear(cls):
        """Clear all registrations (for testing only)."""
        cls._components.clear()
        cls._component_map.clear()
        logger.debug("registry_cleared")


def register_component(component_class: Type[BaseLifecycleComponent]) -> Type[BaseLifecycleComponent]:
    """
    Decorator for automatic component registration.
    
    Usage:
        @register_component
        class MyComponent(BaseLifecycleComponent):
            name = "MyService"
            priority = ComponentPriority.HIGH
            
            async def startup(self):
                ...
    """
    return ComponentRegistry.register(component_class)
