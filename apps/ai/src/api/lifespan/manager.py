"""Main lifecycle manager orchestrating all component startup and shutdown."""
from contextlib import asynccontextmanager
from typing import List, Optional, Set, Dict
import asyncio
import structlog
from datetime import datetime

from .base import BaseLifecycleComponent, ComponentState
from .registry import ComponentRegistry
from .health_registry import get_health_registry, HealthStatus
from ...core.config import settings

logger = structlog.get_logger("lifespan_manager")


class LifespanManager:
    """
    Enterprise lifecycle manager for SSSP AI Service.
    
    Responsibilities:
    - Component instantiation and validation
    - Dependency-aware startup ordering
    - Parallel startup of independent components
    - Timeout protection and error isolation
    - Health monitoring integration
    - Graceful shutdown with cleanup
    - Startup/shutdown metrics collection
    
    Design Principles:
    - Fail fast on critical errors
    - Graceful degradation on non-critical failures
    - Complete error isolation between components
    - Detailed logging and observability
    """
    
    def __init__(
        self,
        startup_timeout: int = 120,
        shutdown_timeout: int = 30,
        parallel_startup: bool = True,
        fail_on_component_error: bool = False
    ):
        """
        Initialize the lifespan manager.
        
        Args:
            startup_timeout: Global startup timeout in seconds
            shutdown_timeout: Global shutdown timeout in seconds
            parallel_startup: Enable parallel startup of independent components
            fail_on_component_error: If True, stop startup on first component failure
        """
        self.startup_timeout = startup_timeout
        self.shutdown_timeout = shutdown_timeout
        self.parallel_startup = parallel_startup
        self.fail_on_component_error = fail_on_component_error
        
        self.components: List[BaseLifecycleComponent] = []
        self.health_registry = get_health_registry()
        
        # Metrics
        self.startup_start_time: Optional[datetime] = None
        self.startup_duration: Optional[float] = None
        self.successful_components: int = 0
        self.failed_components: int = 0
    
        async def initialize_components(self) -> None:
            """
            Discover, validate, and instantiate all registered components.

            Steps:
            1. Validate all dependencies
            2. Get components in dependency batches
            3. Instantiate each component (async-safe)
            4. Register with health system

            Raises:
                ValueError: If dependencies are invalid
                RuntimeError: If instantiation fails critically
            """
            logger.info("initializing_component_registry")

            try:
                # Step 1: Validate dependency graph
                ComponentRegistry.validate_dependencies()
                logger.debug("Dependency graph validated successfully")

                # Step 2: Retrieve component classes sorted by dependency batches
                component_classes = ComponentRegistry.get_sorted_by_priority()
                if not component_classes:
                    raise RuntimeError("No components registered in the ComponentRegistry")

                batches: List[List[type[BaseLifecycleComponent]]] = []
                started: Set[str] = set()

                # Step 3: Generate dependency batches
                dependency_batches = ComponentRegistry.get_dependency_batches(component_classes)

                for batch in dependency_batches:
                    if not batch:
                        remaining = [c.name for c in self.components if c.name not in started]
                        raise RuntimeError(
                            f"Circular dependency or invalid registry state. Remaining: {remaining}"
                        )

                    # Sort batch by (priority, name)
                    batch.sort(key=lambda c: (c.priority.value, c.name))
                    batches.append(batch)
                    started.update(c.name for c in batch)

                logger.info(
                    "component_batches_created",
                    total_batches=len(batches),
                    batch_summary=[
                        {"index": i + 1, "components": [c.name for c in batch]}
                        for i, batch in enumerate(batches)
                    ]
                )

                # Step 4: Instantiate components and register with health system
                for batch_index, batch in enumerate(batches, start=1):
                    logger.info(
                        "instantiating_batch",
                        batch=batch_index,
                        components=[c.name for c in batch]
                    )

                    for component_cls in batch:
                        try:
                            instance = component_cls()
                            self.components.append(instance)
                            self.health_registry.register(instance)
                            logger.debug(f"Component instantiated and registered: {component_cls.name}")
                        except Exception as e:
                            logger.error(
                                "component_instantiation_failed",
                                component=component_cls.__name__,
                                error=str(e),
                                exc_info=True
                            )
                            if self.fail_on_component_error:
                                raise RuntimeError(
                                    f"Failed to instantiate component {component_cls.__name__}"
                                ) from e

                logger.info(
                    "component_registry_initialized",
                    total_components=len(self.components),
                    batches=len(batches)
                )

            except ValueError as e:
                logger.error("Dependency validation failed", error=str(e), exc_info=True)
                raise

            except Exception as e:
                logger.critical("Critical error initializing components", error=str(e), exc_info=True)
                raise RuntimeError("Component initialization failed critically") from e

    async def startup_all(self) -> bool:
        """
        Start all components in dependency order.
        
        Returns:
            True if all components started successfully, False otherwise
        """
        self.startup_start_time = datetime.utcnow()
        
        logger.info(
            "starting_all_components",
            total_components=len(self.components),
            parallel_mode=self.parallel_startup,
            fail_fast=self.fail_on_component_error
        )
        
        try:
            # Calculate startup batches
            batches = self._get_startup_batches()
            
            logger.info(
                "startup_batches_calculated",
                total_batches=len(batches),
                batches_summary=[
                    {
                        "batch": i + 1,
                        "components": [c.name for c in batch],
                        "count": len(batch)
                    }
                    for i, batch in enumerate(batches)
                ]
            )
            
            failed_components = []
            
            # Process each batch
            for batch_index, batch in enumerate(batches, 1):
                logger.info(
                    "starting_batch",
                    batch=batch_index,
                    total_batches=len(batches),
                    components=[c.name for c in batch],
                    count=len(batch)
                )
                
                if self.parallel_startup and len(batch) > 1:
                    # Start batch in parallel
                    results = await asyncio.gather(
                        *[self._startup_component(c) for c in batch],
                        return_exceptions=False
                    )
                    
                    # Count successes/failures
                    for i, success in enumerate(results):
                        if success:
                            self.successful_components += 1
                        else:
                            self.failed_components += 1
                            failed_components.append(batch[i].name)
                            
                            # Fail fast if configured
                            if self.fail_on_component_error:
                                raise RuntimeError(
                                    f"Component {batch[i].name} failed to start"
                                )
                else:
                    # Start batch sequentially
                    for component in batch:
                        success = await self._startup_component(component)
                        
                        if success:
                            self.successful_components += 1
                        else:
                            self.failed_components += 1
                            failed_components.append(component.name)
                            
                            if self.fail_on_component_error:
                                raise RuntimeError(
                                    f"Component {component.name} failed to start"
                                )
                
                logger.info(
                    "batch_completed",
                    batch=batch_index,
                    successful=len([c for c in batch if c.state == ComponentState.READY]),
                    failed=len([c for c in batch if c.state == ComponentState.FAILED])
                )
            
            # Calculate final metrics
            self.startup_duration = (
                datetime.utcnow() - self.startup_start_time
            ).total_seconds()
            
            all_successful = len(failed_components) == 0
            
            if all_successful:
                logger.info(
                    "startup_completed_successfully",
                    total_components=len(self.components),
                    duration_seconds=self.startup_duration
                )
            else:
                logger.warning(
                    "startup_completed_with_failures",
                    total_components=len(self.components),
                    successful=self.successful_components,
                    failed=self.failed_components,
                    failed_components=failed_components,
                    duration_seconds=self.startup_duration
                )
            
            return all_successful
            
        except Exception as e:
            logger.critical(
                "startup_critical_error",
                error=str(e),
                successful_components=self.successful_components,
                failed_components=self.failed_components,
                exc_info=True
            )
            raise
    
    async def shutdown_all(self) -> None:
        """
        Shut down all components in reverse order (LIFO).
        
        Components that failed to start are skipped.
        Errors during shutdown are isolated and logged.
        """
        logger.info(
            "shutting_down_all_components",
            total_components=len(self.components)
        )
        
        shutdown_start = datetime.utcnow()
        
        # Shutdown in reverse order
        for component in reversed(self.components):
            await self._shutdown_component(component)
        
        shutdown_duration = (datetime.utcnow() - shutdown_start).total_seconds()
        
        logger.info(
            "shutdown_completed",
            duration_seconds=shutdown_duration,
            total_components=len(self.components)
        )
    
    async def health_check_all(self) -> Dict[str, bool]:
        """
        Run health checks on all components.
        
        Returns:
            Dictionary mapping component names to health status (True=healthy)
        """
        results = {}
        
        for component in self.components:
            try:
                is_healthy = await component.health_check()
                results[component.name] = is_healthy
                
                if not is_healthy and component.state == ComponentState.READY:
                    self.health_registry.mark_degraded(
                        component.name,
                        "Health check failed"
                    )
                    
            except Exception as e:
                results[component.name] = False
                logger.error(
                    "health_check_error",
                    component=component.name,
                    error=str(e),
                    exc_info=True
                )
                self.health_registry.mark_failed(
                    component.name,
                    f"Health check error: {e}"
                )
        
        return results
    
    def get_startup_metrics(self) -> Dict[str, any]:
        """Get startup performance metrics."""
        return {
            "startup_duration_seconds": self.startup_duration,
            "total_components": len(self.components),
            "successful_components": self.successful_components,
            "failed_components": self.failed_components,
            "startup_timestamp": self.startup_start_time.isoformat() if self.startup_start_time else None,
        }


@asynccontextmanager
async def lifespan(app):
    """
    FastAPI lifespan context manager.
    
    Integrates enterprise lifecycle management with FastAPI.
    
    Startup:
    1. Initialize component registry
    2. Validate dependencies
    3. Start components in dependency order
    4. Attach manager to app state
    
    Shutdown:
    1. Stop components in reverse order
    2. Clean up resources
    3. Log final metrics
    """
    logger.info(
        "application_starting",
        app_name=getattr(settings, 'APP_NAME', 'SSSP AI Service'),
        version=getattr(settings, 'APP_VERSION', 'unknown'),
        environment=getattr(settings, 'ENVIRONMENT', 'development'),
    )
    
    # Create lifecycle manager with settings
    manager = LifespanManager(
        startup_timeout=getattr(settings, 'STARTUP_TIMEOUT', 120),
        shutdown_timeout=getattr(settings, 'SHUTDOWN_TIMEOUT', 30),
        parallel_startup=getattr(settings, 'PARALLEL_STARTUP', True),
        fail_on_component_error=getattr(settings, 'FAIL_ON_COMPONENT_ERROR', False)
    )
    
    try:
        # Initialize all registered components
        await manager.initialize_components()
        
        # Start all components
        all_started = await manager.startup_all()
        
        if not all_started:
            logger.warning(
                "application_started_with_degraded_components",
                message="Some components failed. Check /health endpoint for details.",
                metrics=manager.get_startup_metrics()
            )
        else:
            logger.info(
                "application_started_successfully",
                metrics=manager.get_startup_metrics()
            )
        
        # Make manager available to the app
        app.state.lifespan_manager = manager
        
        # Yield control to FastAPI
        yield
        
    except Exception as e:
        logger.critical(
            "application_startup_failed",
            error=str(e),
            exc_info=True
        )
        # Re-raise to prevent app from starting in broken state
        raise
    
    finally:
        # Graceful shutdown
        logger.info("application_shutting_down")
        
        try:
            await manager.shutdown_all()
            logger.info("application_shutdown_completed")
        except Exception as e:
            logger.error(
                "shutdown_error",
                error=str(e),
                exc_info=True
            )
