"""Concrete component implementations."""
# This file imports all modules to trigger @register_component decorators
from api.lifespan.modules.detection import DetectionModelComponent
from api.lifespan.modules.grpc_server import GRPCServerComponent
#from .rabbitmq import RabbitMQComponent

__all__ = [
    'DetectionModelComponent',
    'GRPCServerComponent', 
    #'RabbitMQComponent',
]
