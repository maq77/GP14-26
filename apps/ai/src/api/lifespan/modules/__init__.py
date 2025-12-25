"""Concrete component implementations."""
# This file imports all modules to trigger @register_component decorators
from .detection import DetectionModelComponent
from .grpc_server import GRPCServerComponent
#from .rabbitmq import RabbitMQComponent

__all__ = [
    'DetectionModelComponent',
    'GRPCServerComponent', 
    #'RabbitMQComponent',
]
