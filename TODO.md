# List of things to implement

## Purpose

I want to have a system which receives home router logs and sends back security notifications.

Home devices like PCs, mobiles, or TVs should be able to get router logs periodically and send them to the system.

Security event to notify:
* New connected device with unknown IP / MAC 

Notifications should go to the mobile application.

## Authentication and authorization

The logs ingestion service should have a secure authentication and authorization mechanism to ensure that only authorized users can access the service and its data.

As for prototype, implement basic authentication using username and password.

Later, consider using more secure methods such as OAuth or JWT.

Logs ingestion service should use now it's own PostgreSQL database to store user credentials and permissions. This will allow scalability as the service grows.

To consider later, create a separate authentication service that can be used by multiple ingestion services in the future. This will allow for better separation of concerns and easier maintenance.

## Health check

Implement a health check endpoint that can be used to monitor the status of the logs ingestion service. This endpoint should return a simple response indicating whether the service is healthy or not.

## Logging and monitoring

Implement logging and monitoring for the logs ingestion service to track its performance and identify any issues that may arise. This can include logging important events, errors, and performance metrics.

To consider later, expose metrics such as request latency, error rates, and throughput. Expose Prometheus endpoint.

## Cache management

Implement a cache management system to improve the performance of the logs ingestion service. Use Redis as a caching layer to store frequently accessed data and reduce the load on the PostgreSQL database.

## Queue management

Implement a queue management system to handle incoming log data and ensure that it is processed efficiently. Use RabbitMQ as a message broker to manage the queues and ensure that log data is processed in a timely manner.

Separate the `IEventPublisher` to a different library and implement RabbitMQ for that.

## Logs consumer

Implement a logs consumer microservice that can process the incoming log data and generate security notifications based on the defined rules. This consumer should be able to handle a high volume of log data and generate notifications in real-time.

Try to implement that in Go.

## docker compose

Implement a Docker Compose file to test entire system locally.

For production, consider using Kubernetes to manage the deployment and scaling of the logs ingestion service. This will allow for better scalability and reliability as the service grows.

## AWS, Azure, GCP

Consider using cloud services such as AWS, Azure, or GCP to deploy and manage the logs ingestion service.