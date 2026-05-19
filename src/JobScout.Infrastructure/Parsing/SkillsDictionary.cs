namespace JobScout.Infrastructure.Parsing;

internal static class SkillsDictionary
{
    // Matched case-insensitively against resume text.
    // Ordered so multi-word terms appear before their single-word components
    // (e.g. "ASP.NET Core" before "ASP.NET" before ".NET").
    internal static readonly string[] Terms =
    [
        // ── Languages ──────────────────────────────────────────────────────────
        "C#", "F#", "VB.NET",
        "TypeScript", "JavaScript", "CoffeeScript",
        "Python", "Ruby", "Perl", "PHP", "Lua", "Groovy",
        "Java", "Kotlin", "Scala", "Clojure",
        "Swift", "Objective-C",
        "Go", "Rust", "Zig",
        "C++", "C",
        "Dart", "Elixir", "Haskell", "Erlang",
        "R", "MATLAB", "Julia",
        "Bash", "PowerShell",

        // ── .NET ecosystem ─────────────────────────────────────────────────────
        "ASP.NET Core", "ASP.NET MVC", "ASP.NET", ".NET Core", ".NET MAUI",
        ".NET", "Blazor", "Razor", "SignalR",
        "Entity Framework Core", "Entity Framework", "EF Core", "Dapper",
        "WPF", "WinForms", "XAML",
        "Orleans", "MediatR", "AutoMapper",

        // ── Web / Frontend frameworks ──────────────────────────────────────────
        "Next.js", "Nuxt.js", "Nuxt", "Remix",
        "React", "Angular", "Vue.js", "Vue", "Svelte",
        "Ember.js", "Backbone.js",
        "jQuery", "Alpine.js",
        "Tailwind CSS", "Bootstrap", "Material UI", "Chakra UI",
        "Sass", "LESS", "CSS", "HTML",
        "Webpack", "Vite", "Rollup", "Parcel", "esbuild",
        "GraphQL", "REST", "gRPC", "WebSockets", "SOAP",

        // ── Backend frameworks ─────────────────────────────────────────────────
        "Spring Boot", "Spring", "Quarkus", "Micronaut",
        "Django", "Flask", "FastAPI", "Tornado",
        "Express.js", "NestJS", "Fastify", "Hapi",
        "Laravel", "Symfony", "CodeIgniter",
        "Ruby on Rails", "Sinatra",
        "Gin", "Fiber", "Echo",
        "Actix", "Axum", "Rocket",
        "Phoenix",

        // ── Databases ──────────────────────────────────────────────────────────
        "SQL Server", "PostgreSQL", "MySQL", "MariaDB", "SQLite", "Oracle",
        "MongoDB", "DynamoDB", "Cosmos DB", "Firestore", "CouchDB",
        "Redis", "Memcached",
        "Cassandra", "ScyllaDB", "HBase",
        "Elasticsearch", "OpenSearch", "Solr",
        "Neo4j", "ArangoDB",
        "InfluxDB", "TimescaleDB", "Prometheus",
        "Snowflake", "BigQuery", "Redshift",

        // ── Cloud platforms ────────────────────────────────────────────────────
        "Azure", "AWS", "GCP", "Google Cloud",
        "Azure Functions", "Azure DevOps", "Azure Kubernetes Service", "AKS",
        "App Service", "Azure Service Bus", "Azure Storage", "Azure AD",
        "Lambda", "EC2", "S3", "ECS", "EKS", "RDS", "CloudFront", "SQS", "SNS",
        "Cloud Run", "Cloud Functions", "GKE",
        "Cloudflare", "Vercel", "Netlify", "Heroku",

        // ── Containers & orchestration ─────────────────────────────────────────
        "Kubernetes", "Docker", "Docker Compose",
        "Helm", "ArgoCD", "Flux", "Istio", "Linkerd",
        "OpenShift",

        // ── Infrastructure as Code ─────────────────────────────────────────────
        "Terraform", "Pulumi", "Ansible", "Chef", "Puppet",
        "CloudFormation", "Bicep", "ARM Templates",

        // ── CI/CD & DevOps tools ───────────────────────────────────────────────
        "GitHub Actions", "GitLab CI", "Jenkins", "CircleCI", "Travis CI",
        "Azure Pipelines", "TeamCity", "Bamboo",
        "Git", "GitHub", "GitLab", "Bitbucket",

        // ── Observability ──────────────────────────────────────────────────────
        "Grafana", "Datadog", "Splunk", "New Relic", "Dynatrace",
        "OpenTelemetry", "Jaeger", "Zipkin",

        // ── Message brokers & streaming ────────────────────────────────────────
        "Apache Kafka", "Kafka", "RabbitMQ", "NATS", "ActiveMQ",
        "Azure Service Bus", "Amazon SQS",

        // ── Data & ML ─────────────────────────────────────────────────────────
        "Machine Learning", "Deep Learning", "MLOps",
        "TensorFlow", "PyTorch", "Keras", "scikit-learn",
        "Pandas", "NumPy", "SciPy", "Matplotlib",
        "Apache Spark", "Databricks", "dbt", "Airflow", "Prefect",
        "Power BI", "Tableau", "Looker", "Metabase",
        "OpenAI", "LangChain", "LlamaIndex", "Hugging Face",

        // ── Mobile ────────────────────────────────────────────────────────────
        "iOS", "Android",
        "React Native", "Flutter", "Xamarin", "MAUI",
        "SwiftUI", "Jetpack Compose",

        // ── Security ──────────────────────────────────────────────────────────
        "OAuth", "OpenID Connect", "JWT", "SAML",
        "SSL", "TLS", "mTLS",
        "OWASP", "Penetration Testing", "Zero Trust",

        // ── Testing ────────────────────────────────────────────────────────────
        "xUnit", "NUnit", "MSTest",
        "Jest", "Vitest", "Mocha", "Jasmine",
        "Pytest", "Unittest",
        "Selenium", "Playwright", "Cypress",
        "Moq", "NSubstitute", "FakeItEasy",
        "k6", "Gatling", "JMeter",

        // ── Architecture & practices ───────────────────────────────────────────
        "Microservices", "Serverless", "Event-Driven", "Domain-Driven Design",
        "CQRS", "Event Sourcing", "SOLID", "Clean Architecture",
        "CI/CD", "DevOps", "SRE", "Agile", "Scrum", "Kanban", "SAFe",
        "API Gateway", "Service Mesh",
    ];
}
