# ---------- Stage 1: Build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy just the csproj first so restore is cached separately from source changes
COPY BirdReportLambda.csproj .
RUN dotnet restore

# Now copy the rest of this folder's contents 
COPY . .
RUN dotnet publish -c Release -o /app/publish

# ---------- Stage 2: Final runtime image ----------
FROM public.ecr.aws/lambda/dotnet:8

# Amazon Linux base
RUN dnf install -y python3 python3-pip && dnf clean all

# Copy the published .NET app
COPY --from=build /app/publish ${LAMBDA_TASK_ROOT}

# Copy the MCP server and install its Python dependencies
COPY MCP ${LAMBDA_TASK_ROOT}/MCP
RUN pip3 install --no-cache-dir -r ${LAMBDA_TASK_ROOT}/MCP/requirements.txt --target ${LAMBDA_TASK_ROOT}/MCP/site-packages

ENV PYTHONPATH=${LAMBDA_TASK_ROOT}/MCP/site-packages

CMD ["Bird-ReportLambda::Function::FunctionHandler"]