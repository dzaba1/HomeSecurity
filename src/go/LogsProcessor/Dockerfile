# Build context is the repo root, like the .NET services' Dockerfiles (see
# docker-compose.yml's `build.context` for the logs-processor entry). Unlike
# them, this image needs nothing outside src/go/LogsProcessor: the Go bindings
# for the JSON Schema contract are generated ahead of time (`go generate`) and
# committed, so the schema files are not needed at build time.

FROM golang:1.27 AS build
WORKDIR /source

# Dependencies first so they stay cached until go.mod/go.sum change.
COPY ["src/go/LogsProcessor/go.mod", "src/go/LogsProcessor/go.sum", "./"]
RUN go mod download

COPY src/go/LogsProcessor/ ./
RUN CGO_ENABLED=0 go build -trimpath -ldflags="-s -w" -o /out/logs-processor ./cmd/logs-processor

# Static binary, so a distroless image with no shell or package manager is
# enough. It also has no curl, hence the binary's own -healthcheck flag (used
# by docker-compose's healthcheck) instead of curl against /health/ready.
FROM gcr.io/distroless/static-debian12:nonroot AS final
COPY --from=build /out/logs-processor /app/logs-processor
USER nonroot:nonroot
EXPOSE 8080
ENTRYPOINT ["/app/logs-processor"]
