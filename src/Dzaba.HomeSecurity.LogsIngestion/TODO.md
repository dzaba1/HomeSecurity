# List of things to implement

## Authentication and authorization

The logs ingestion service should have a secure authentication and authorization mechanism to ensure that only authorized users can access the service and its data.

As for prototype, implement basic authentication using username and password. Later, consider using more secure methods such as OAuth or JWT.

Logs ingestion service should use now it's own PostgreSQL database to store user credentials and permissions. This will allow scalability as the service grows.

To consider later, create a separate authentication service that can be used by multiple ingestion services in the future. This will allow for better separation of concerns and easier maintenance.

## Health check

Implement a health check endpoint that can be used to monitor the status of the logs ingestion service. This endpoint should return a simple response indicating whether the service is healthy or not.

## Logging and monitoring

Implement logging and monitoring for the logs ingestion service to track its performance and identify any issues that may arise. This can include logging important events, errors, and performance metrics.

To consider later, expose metrics such as request latency, error rates, and throughput. Expose Prometheus endpoint.

## Cache management

Implement a cache management system to improve the performance of the logs ingestion service. Use Redis as a caching layer to store frequently accessed data and reduce the load on the PostgreSQL database.

# Queue management

Implement a queue management system to handle incoming log data and ensure that it is processed efficiently. Use RabbitMQ as a message broker to manage the queues and ensure that log data is processed in a timely manner.

Separate the `IEventPublisher` to a different library and implement RabbitMQ for that.
