# ---------- Stage 1: Build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Bird-Report.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish

# ---------- Stage 2: Final runtime image ----------
FROM public.ecr.aws/lambda/dotnet:10

RUN dnf install -y python3.11 python3.11-pip && dnf clean all

COPY --from=build /app/publish ${LAMBDA_TASK_ROOT}

COPY MCP ${LAMBDA_TASK_ROOT}/MCP
RUN python3.11 -m pip install --no-cache-dir -r ${LAMBDA_TASK_ROOT}/MCP/requirements.txt --target ${LAMBDA_TASK_ROOT}/MCP/site-packages

ENV PYTHONPATH=${LAMBDA_TASK_ROOT}/MCP/site-packages

CMD ["Bird-Report::Function::FunctionHandler"]