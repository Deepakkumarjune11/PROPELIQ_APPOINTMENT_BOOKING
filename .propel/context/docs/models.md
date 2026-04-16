# Design Modelling

## UML Models Overview

This document provides comprehensive UML visual models for the Unified Patient Access & Clinical Intelligence Platform. The diagrams translate requirements from [spec.md](.propel/context/docs/spec.md) and architectural decisions from [design.md](.propel/context/docs/design.md) into visual representations that guide implementation.

**Diagram Organization:**
- **System Context**: Shows the platform boundary and interactions with external actors (patients, staff, admins) and services (Azure OpenAI, calendar APIs, notification services)
- **Component Architecture**: Visualizes the modular monolith structure with patient-access and clinical-intelligence bounded contexts
- **Deployment Architecture**: Documents Windows/IIS hosting with PostgreSQL, Upstash Redis, and Azure OpenAI infrastructure
- **Data Flow**: Traces data movement through booking workflows, document processing pipeline, and RAG-based extraction
- **Logical Data Model (ERD)**: Defines the 10 core entities and their relationships
- **Sequence Diagrams**: One diagram per use case (UC-001 through UC-006) showing detailed interaction flows

These models support development, testing, and operational understanding of how the platform achieves 99.9% uptime, 2-minute chart prep, and >98% AI-human agreement targets.

## Architectural Views

### System Context Diagram
```plantuml
@startuml
!define RECTANGLE class

skinparam componentStyle rectangle
skinparam backgroundColor #FEFEFE
skinparam defaultFontSize 11

package "Unified Patient Access & Clinical Intelligence Platform" <<system>> {
  [Patient Access Module] as PAM
  [Clinical Intelligence Module] as CIM
  [Admin Module] as ADM
  database "PostgreSQL + pgvector" as DB
  database "Upstash Redis Cache" as Cache
}

actor "Patient" as Patient #LightBlue
actor "Staff" as Staff #LightGreen
actor "Admin" as Admin #LightCoral

cloud "Azure OpenAI Service" as AzureAI {
  [GPT-4 Turbo] as GPT4
  [text-embedding-3-small] as Embedding
  [Content Safety] as Safety
}

cloud "External Services" as ExtSvc {
  [Google Calendar API] as GCal
  [Outlook Calendar API] as Outlook
  [Twilio SMS] as SMS
  [SendGrid Email] as Email
}

Patient --> PAM : Book appointments\nComplete intake\nUpload documents
Staff --> PAM : Walk-in management\nQueue handling\nArrivals
Staff --> CIM : Review extractions\nResolve conflicts\nConfirm codes
Admin --> ADM : User management\nRole assignment\nAccess control

PAM --> DB : Store appointments\nIntake responses
PAM --> Cache : Session state\nAvailability cache
PAM --> ExtSvc : Send reminders\nSync calendars\nPDF confirmations

CIM --> DB : Store documents\nExtracted facts\n360-view
CIM --> AzureAI : Extract clinical data\nDetect conflicts\nSuggest codes
CIM --> Cache : Embedding cache

ADM --> DB : Audit log\nUser accounts
AzureAI --> Safety : Content filtering
DB --> PAM : Patient data\nAppointments
DB --> CIM : Documents\nFacts

note right of AzureAI
  HIPAA BAA signed
  Token budget: 8,000/request
  Circuit breaker enabled
end note

note bottom of DB
  Primary data store:
  - Relational data (EF Core)
  - Vector embeddings (pgvector)
  - Immutable audit log
end note

@enduml
```

### Component Architecture Diagram
```mermaid
graph TB
    subgraph "Frontend - React SPA"
        UI[React Components]
        State[React Query + Zustand]
        Auth[Auth Context]
    end

    subgraph "Backend - .NET 8 API"
        subgraph "Patient Access Bounded Context"
            PAC_Pres[Presentation Layer]
            PAC_App[Application Layer]
            PAC_Domain[Domain Layer]
            PAC_Data[Data Layer]
        end

        subgraph "Clinical Intelligence Bounded Context"
            CIC_Pres[Presentation Layer]
            CIC_App[Application Layer]
            CIC_Domain[Domain Layer]
            CIC_Data[Data Layer]
        end

        subgraph "Admin Bounded Context"
            ADM_Pres[Presentation Layer]
            ADM_App[Application Layer]
            ADM_Domain[Domain Layer]
            ADM_Data[Data Layer]
        end

        subgraph "Shared Kernel"
            Identity[ASP.NET Core Identity]
            AIGateway[Custom AI Gateway Middleware]
            AuditLog[Audit Logging Interceptor]
            Encryption[Data Protection API]
            BgJobs[Hangfire Background Jobs]
        end
    end

    subgraph "Data Tier"
        PG[(PostgreSQL 15<br/>+ pgvector)]
        Redis[(Upstash Redis)]
    end

    subgraph "External Integration Layer"
        AzureAI[Azure OpenAI Client]
        CalendarAPI[Calendar API Clients]
        NotifAPI[Notification API Clients]
    end

    UI --> State
    UI --> Auth
    State --> PAC_Pres
    State --> CIC_Pres
    State --> ADM_Pres
    Auth --> Identity

    PAC_Pres --> PAC_App
    PAC_App --> PAC_Domain
    PAC_App --> BgJobs
    PAC_Domain --> PAC_Data
    PAC_Data --> PG
    PAC_App --> Redis
    PAC_App --> CalendarAPI
    PAC_App --> NotifAPI

    CIC_Pres --> CIC_App
    CIC_App --> CIC_Domain
    CIC_App --> AIGateway
    CIC_App --> BgJobs
    CIC_Domain --> CIC_Data
    CIC_Data --> PG
    CIC_App --> Redis
    AIGateway --> AzureAI

    ADM_Pres --> ADM_App
    ADM_App --> ADM_Domain
    ADM_Domain --> ADM_Data
    ADM_Data --> PG

    Identity --> PG
    AuditLog --> PG
    Encryption --> PG
    BgJobs --> PG

    classDef frontend fill:#E3F2FD,stroke:#1976D2,stroke-width:2px
    classDef backend fill:#F3E5F5,stroke:#7B1FA2,stroke-width:2px
    classDef data fill:#E8F5E9,stroke:#388E3C,stroke-width:2px
    classDef external fill:#FFF3E0,stroke:#F57C00,stroke-width:2px
    classDef shared fill:#FFF9C4,stroke:#F9A825,stroke-width:2px

    class UI,State,Auth frontend
    class PAC_Pres,PAC_App,PAC_Domain,PAC_Data,CIC_Pres,CIC_App,CIC_Domain,CIC_Data,ADM_Pres,ADM_App,ADM_Domain,ADM_Data backend
    class Identity,AIGateway,AuditLog,Encryption,BgJobs shared
    class PG,Redis data
    class AzureAI,CalendarAPI,NotifAPI external
```

### Deployment Architecture Diagram
```plantuml
@startuml
!include <C4/C4_Deployment>

LAYOUT_LEFT_RIGHT()

Deployment_Node(client, "Client Devices", "Windows/macOS/iOS/Android") {
    Container(browser, "Web Browser", "Chrome, Edge, Safari, Firefox", "Renders React SPA")
}

Deployment_Node(webTier, "Web Tier", "Windows Server 2019") {
    Deployment_Node(iis, "IIS 10", "Application Server") {
        Container(webapp, "React Frontend", "JavaScript/React 18", "Static files served via IIS")
        Container(webapi, ".NET Backend API", "ASP.NET Core 8", "Hosted in IIS App Pool")
    }
}

Deployment_Node(dataTier, "Data Tier", "Cloud Infrastructure") {
    Deployment_Node(pgNode, "PostgreSQL Cluster") {
        ContainerDb(postgres, "PostgreSQL 15", "Primary Database", "Stores relational data\nand vector embeddings (pgvector)")
        ContainerDb(pgReplica, "PostgreSQL Replica", "Read Replica", "Load balancing for read queries")
    }
    
    Deployment_Node(cacheNode, "Cache Layer") {
        ContainerDb(redis, "Upstash Redis", "Managed Redis", "Session state\nQuery cache\nEmbedding cache")
    }
}

Deployment_Node(aiTier, "AI Services", "Azure Cloud") {
    Deployment_Node(openai, "Azure OpenAI Service", "HIPAA BAA Enabled") {
        Container(gpt4, "GPT-4 Turbo", "LLM", "Clinical extraction\nConversational intake\nCode suggestions")
        Container(embedding, "text-embedding-3-small", "Embedding Model", "Document chunking\nVector generation")
        Container(safety, "Content Safety", "Moderation", "Harmful content filtering")
    }
}

Deployment_Node(extServices, "External Services", "Third-Party APIs") {
    Container(gcal, "Google Calendar", "OAuth 2.0", "Calendar sync")
    Container(outlook, "Outlook Calendar", "Microsoft Graph", "Calendar sync")
    Container(twilio, "Twilio SMS", "Free Tier", "Reminder delivery")
    Container(sendgrid, "SendGrid Email", "Free Tier", "Confirmation emails")
}

Rel(browser, webapp, "HTTPS", "443")
Rel(webapp, webapi, "REST API", "HTTPS 443")
Rel(webapi, postgres, "SQL", "TCP 5432")
Rel(webapi, redis, "Redis Protocol", "TLS 6379")
Rel(webapi, gpt4, "HTTPS", "443 via AI Gateway")
Rel(webapi, embedding, "HTTPS", "443 via AI Gateway")
Rel(gpt4, safety, "Internal", "Pre/post filtering")
Rel(webapi, gcal, "REST API", "OAuth 2.0")
Rel(webapi, outlook, "REST API", "OAuth 2.0")
Rel(webapi, twilio, "REST API", "HTTPS")
Rel(webapi, sendgrid, "REST API", "HTTPS")
Rel(postgres, pgReplica, "Replication", "WAL streaming")

note right of iis
  Windows Services alternative
  for background jobs and
  polling services
end note

note bottom of postgres
  Backup Strategy:
  - Daily incremental
  - Weekly full
  - 7-year retention
  - RPO: 1 hour, RTO: 4 hours
end note

@enduml
```

### Data Flow Diagram
```plantuml
@startuml
!define RECTANGLE_STYLE skinparam roundcorner 10

skinparam defaultFontSize 10
skinparam backgroundColor #FEFEFE

title Data Flow - Booking and Clinical Intelligence Workflows

' External entities
rectangle "Patient" as Patient #LightBlue
rectangle "Staff" as Staff #LightGreen
rectangle "Azure OpenAI" as AI #FFE0B2

' Processes
rectangle "P1\nBook Appointment" as P1 #E3F2FD
rectangle "P2\nComplete Intake" as P2 #E3F2FD
rectangle "P3\nUpload Document" as P3 #E1F5FE
rectangle "P4\nExtract Clinical Data" as P4 #E1F5FE
rectangle "P5\nAssemble 360 View" as P5 #E1F5FE
rectangle "P6\nDetect Conflicts" as P6 #E1F5FE
rectangle "P7\nSuggest Codes" as P7 #E1F5FE
rectangle "P8\nVerify & Confirm" as P8 #E8F5E9
rectangle "P9\nSend Reminders" as P9 #FFF3E0
rectangle "P10\nManage Swap Watchlist" as P10 #FFF3E0
rectangle "P11\nCalculate No-Show Risk" as P11 #FFF3E0

' Data stores
database "D1\nPatient DB" as D1 #C8E6C9
database "D2\nAppointment DB" as D2 #C8E6C9
database "D3\nIntake DB" as D3 #C8E6C9
database "D4\nDocument Store" as D4 #B3E5FC
database "D5\nExtracted Facts DB" as D5 #B3E5FC
database "D6\nPatient View DB" as D6 #B3E5FC
database "D7\nCode Suggestions DB" as D7 #B3E5FC
database "D8\nAudit Log" as D8 #FFCCBC
database "D9\nRedis Cache" as D9 #FFF9C4
database "D10\nVector Store\n(pgvector)" as D10 #B2DFDB

' External outputs
rectangle "Google/Outlook\nCalendar" as ExtCal #FCE4EC
rectangle "SMS/Email\nServices" as ExtNotif #FCE4EC

' Flow 1: Booking workflow
Patient --> P1 : Search availability
P1 --> D9 : Check cached slots
P1 --> D2 : Read available slots
P1 --> P11 : Patient signals
P11 --> D1 : Read history
P11 --> D9 : Cache risk score
P1 --> D2 : Create appointment
P1 --> D1 : Update patient
P1 --> D8 : Log booking event

P2 --> P1 : Intake data
Patient --> P2 : Submit answers\n(conversational/manual)
P2 --> D3 : Store intake response
P2 --> D8 : Log intake event

P1 --> P9 : Trigger confirmation
P9 --> ExtNotif : Send SMS/Email
P9 --> ExtCal : Create calendar event
P9 --> D8 : Log notification

P1 --> P10 : Register preferred slot
P10 --> D2 : Monitor slot changes
P10 --> P1 : Swap on availability

' Flow 2: Document processing pipeline
Patient --> P3 : Upload PDF
Staff --> P3 : Associate with encounter
P3 --> D4 : Store original file
P3 --> D8 : Log upload

P3 --> P4 : Queue extraction job
P4 --> D10 : Chunk + embed document
P4 --> AI : Extract facts via RAG
AI --> P4 : Structured output
P4 --> D5 : Store extracted facts\n+ confidence scores
P4 --> D8 : Log extraction

P4 --> P5 : New facts available
P5 --> D5 : Read all facts
P5 --> D10 : Semantic similarity search
P5 --> D6 : Update 360 view (JSONB)
P5 --> P6 : Check for conflicts

P6 --> D5 : Compare fact values
P6 --> D6 : Flag conflicts
P6 --> D8 : Log conflict detection

P5 --> P7 : Consolidated view ready
P7 --> AI : Generate code suggestions
AI --> P7 : ICD-10 + CPT codes
P7 --> D7 : Store suggestions\n+ evidence links

' Flow 3: Verification workflow
Staff --> P8 : Review patient view
P8 --> D6 : Read 360 view
P8 --> D5 : Read source facts
P8 --> D7 : Read code suggestions
Staff --> P8 : Confirm/reject
P8 --> D6 : Update verification status
P8 --> D7 : Update review flags
P8 --> D8 : Log verification

@enduml
```

### Logical Data Model (ERD)
```mermaid
erDiagram
    PATIENT ||--o{ APPOINTMENT : has
    PATIENT ||--o{ INTAKE_RESPONSE : completes
    PATIENT ||--o{ CLINICAL_DOCUMENT : uploads
    PATIENT ||--o| PATIENT_VIEW_360 : aggregates
    PATIENT ||--o{ CODE_SUGGESTION : receives
    PATIENT {
        uuid id PK
        string email UK
        string name
        date dob
        string phone
        string insurance_provider
        string insurance_member_id
        string insurance_status
        timestamp created_at
        timestamp updated_at
        boolean is_deleted
    }

    APPOINTMENT ||--o| APPOINTMENT : prefers
    APPOINTMENT {
        uuid id PK
        uuid patient_id FK
        datetime slot_datetime
        string status
        uuid preferred_slot_id FK
        float no_show_risk_score
        timestamp created_at
        timestamp updated_at
        boolean is_deleted
    }

    INTAKE_RESPONSE {
        uuid id PK
        uuid patient_id FK
        string mode
        jsonb answers
        timestamp created_at
    }

    CLINICAL_DOCUMENT ||--o{ EXTRACTED_FACT : contains
    CLINICAL_DOCUMENT {
        uuid id PK
        uuid patient_id FK
        uuid encounter_id FK
        string file_reference
        string extraction_status
        timestamp uploaded_at
        timestamp processed_at
    }

    EXTRACTED_FACT ||--o{ PATIENT_VIEW_360 : consolidates
    EXTRACTED_FACT ||--o{ CODE_SUGGESTION : supports
    EXTRACTED_FACT {
        uuid id PK
        uuid document_id FK
        string fact_type
        string value
        float confidence_score
        int source_char_offset
        int source_char_length
        timestamp extracted_at
    }

    PATIENT_VIEW_360 {
        uuid id PK
        uuid patient_id FK
        jsonb consolidated_facts
        string[] conflict_flags
        string verification_status
        timestamp last_updated
        int version
    }

    CODE_SUGGESTION {
        uuid id PK
        uuid patient_id FK
        string code_type
        string code_value
        uuid[] evidence_fact_ids
        boolean staff_reviewed
        string review_outcome
        timestamp created_at
        timestamp reviewed_at
    }

    STAFF ||--o{ AUDIT_LOG : performs
    ADMIN ||--o{ AUDIT_LOG : performs
    STAFF {
        uuid id PK
        string username
        string role
        int permissions_bitfield
        string auth_credentials
        timestamp created_at
        boolean is_active
    }

    ADMIN {
        uuid id PK
        string username
        int access_privileges
        string auth_credentials
        timestamp created_at
        boolean is_active
    }

    AUDIT_LOG {
        uuid id PK
        uuid actor_id FK
        string actor_type
        string action_type
        string target_entity_type
        uuid target_entity_id
        jsonb payload
        timestamp created_at
    }

    VECTOR_EMBEDDING {
        uuid id PK
        uuid document_id FK
        int chunk_index
        vector embedding
        string chunk_text
        int token_count
        timestamp created_at
    }

    CLINICAL_DOCUMENT ||--o{ VECTOR_EMBEDDING : embeds
```

### Use Case Sequence Diagrams

> **Note**: Each sequence diagram below corresponds to one use case (UC-001 through UC-006) from [spec.md](.propel/context/docs/spec.md). Diagrams show detailed message flows including actors, system components, and data stores interactions.

#### UC-001: Patient Books an Appointment and Completes Intake
**Source**: [spec.md#UC-001](.propel/context/docs/spec.md#UC-001)

```mermaid
sequenceDiagram
    participant Patient
    participant UI as React Frontend
    participant API as .NET Backend API
    participant Cache as Upstash Redis
    participant DB as PostgreSQL
    participant BgJob as Hangfire
    participant ExtSvc as External Services
    participant AI as Azure OpenAI

    Note over Patient,AI: UC-001 - Patient Books Appointment and Completes Intake

    Patient->>UI: Search for available slots
    UI->>API: GET /api/appointments/availability
    API->>Cache: Check cached availability
    alt Cache hit
        Cache-->>API: Return cached slots
    else Cache miss
        API->>DB: Query available appointments
        DB-->>API: Return slots
        API->>Cache: Store availability (TTL 60s)
    end
    API-->>UI: Return available slots
    UI-->>Patient: Display calendar

    Patient->>UI: Select slot + enter details
    UI->>API: POST /api/patients/register
    API->>DB: Create patient record
    DB-->>API: Patient ID
    API->>DB: Soft validate insurance
    DB-->>API: Validation status

    Patient->>UI: Choose intake mode
    alt Conversational intake
        Patient->>UI: Start AI conversation
        UI->>API: POST /api/intake/conversational
        API->>AI: Generate intake prompt
        AI-->>API: Conversational response
        API-->>UI: Display AI question
        Patient->>UI: Provide answer
        UI->>API: POST /api/intake/conversational
        Note right of API: Loop until complete
    else Manual intake
        Patient->>UI: Fill form fields
        UI->>API: POST /api/intake/manual
    end
    API->>DB: Store intake response
    DB-->>API: Intake ID

    API->>DB: Calculate no-show risk score
    DB-->>API: Risk score
    API->>DB: Create appointment transaction
    DB-->>API: Appointment ID
    API->>DB: Write audit log
    
    API->>BgJob: Queue reminder job
    BgJob->>ExtSvc: Send SMS/Email reminder
    BgJob->>ExtSvc: Create calendar events
    BgJob->>ExtSvc: Generate PDF confirmation
    ExtSvc-->>BgJob: Delivery status
    BgJob->>DB: Log notification status

    API-->>UI: Return booking confirmation
    UI-->>Patient: Display success + PDF link
```

#### UC-002: Patient Requests a Preferred Slot Swap
**Source**: [spec.md#UC-002](.propel/context/docs/spec.md#UC-002)

```mermaid
sequenceDiagram
    participant Patient
    participant UI as React Frontend
    participant API as .NET Backend API
    participant DB as PostgreSQL
    participant BgJob as Hangfire
    participant ExtSvc as Notification Services

    Note over Patient,ExtSvc: UC-002 - Patient Requests Preferred Slot Swap

    Patient->>UI: Book available appointment
    UI->>API: POST /api/appointments
    API->>DB: Create appointment (booked)
    DB-->>API: Appointment ID

    Patient->>UI: Select preferred unavailable slot
    UI->>API: POST /api/appointments/{id}/preferred-slot
    API->>DB: Check slot eligibility
    alt Slot is eligible
        DB-->>API: Slot available for watchlist
        API->>DB: Update appointment.preferred_slot_id
        API->>DB: Add to swap watchlist
        DB-->>API: Watchlist entry created
        API-->>UI: Watchlist registered
        UI-->>Patient: "We'll notify you if slot opens"
    else Slot no longer eligible
        DB-->>API: Slot not available
        API-->>UI: Cannot watchlist
        UI-->>Patient: Keep current appointment
    end

    Note over BgJob,ExtSvc: Background monitoring (every 5 minutes)
    BgJob->>DB: Poll swap watchlist
    DB-->>BgJob: Watchlist entries
    BgJob->>DB: Check preferred slot availability
    
    alt Preferred slot now available
        DB-->>BgJob: Slot is free
        BgJob->>DB: BEGIN TRANSACTION
        BgJob->>DB: Lock preferred slot
        BgJob->>DB: Update appointment.slot_datetime
        BgJob->>DB: Release original slot
        BgJob->>DB: Remove from watchlist
        BgJob->>DB: Write audit logs
        BgJob->>DB: COMMIT TRANSACTION
        DB-->>BgJob: Swap successful
        
        BgJob->>ExtSvc: Send swap notification
        ExtSvc-->>BgJob: Notification sent
        BgJob->>DB: Log notification
    else Slot still unavailable
        DB-->>BgJob: No change
    end
```

#### UC-003: Staff Books a Walk-In and Manages Same-Day Arrival
**Source**: [spec.md#UC-003](.propel/context/docs/spec.md#UC-003)

```mermaid
sequenceDiagram
    participant Staff
    participant UI as React Frontend
    participant API as .NET Backend API
    participant DB as PostgreSQL
    participant Cache as Upstash Redis

    Note over Staff,Cache: UC-003 - Staff Books Walk-In and Manages Arrival

    Staff->>UI: Access walk-in booking form
    UI->>API: GET /api/staff/walk-in
    API->>DB: Verify staff permissions
    DB-->>API: Authorized (staff role)

    Staff->>UI: Search for patient by email/phone
    UI->>API: GET /api/patients/search?query={term}
    API->>DB: Query patient records
    
    alt Patient found
        DB-->>API: Patient record
        API-->>UI: Display patient
    else Patient not found
        Staff->>UI: Create new patient account
        UI->>API: POST /api/patients (staff-initiated)
        API->>DB: Create minimal patient profile
        DB-->>API: Patient ID
        API-->>UI: Patient account created
    end

    Staff->>UI: Book walk-in appointment
    UI->>API: POST /api/appointments/walk-in
    API->>DB: Check same-day slots
    
    alt Slot available
        DB-->>API: Slot found
        API->>DB: Create appointment (walk-in status)
        API->>DB: Add to same-day queue
        DB-->>API: Appointment + queue position
        API-->>UI: Walk-in booked
        UI-->>Staff: Display queue position
    else No slots
        DB-->>API: No availability
        API->>DB: Add to wait queue
        DB-->>API: Wait queue entry
        API-->>UI: Added to wait queue
        UI-->>Staff: Patient on wait queue
    end

    API->>DB: Write audit log
    
    Note over Staff,Cache: Patient arrival workflow
    Staff->>UI: View same-day queue
    UI->>API: GET /api/staff/queue
    API->>Cache: Check cached queue
    alt Cache hit
        Cache-->>API: Queue state
    else Cache miss
        API->>DB: Query queue
        DB-->>API: Queue entries
        API->>Cache: Store queue (TTL 30s)
    end
    API-->>UI: Display queue

    Staff->>UI: Mark patient as arrived
    UI->>API: PATCH /api/appointments/{id}/status
    API->>DB: Update appointment.status = 'arrived'
    API->>DB: Update queue state
    API->>DB: Write audit log
    API->>Cache: Invalidate queue cache
    DB-->>API: Status updated
    API-->>UI: Arrival confirmed
    UI-->>Staff: Patient checked in
```

#### UC-004: Patient Uploads Historical or Post-Visit Documents
**Source**: [spec.md#UC-004](.propel/context/docs/spec.md#UC-004)

```mermaid
sequenceDiagram
    participant Patient
    participant UI as React Frontend
    participant API as .NET Backend API
    participant Storage as File Storage
    participant DB as PostgreSQL
    participant BgJob as Hangfire
    participant AI as Azure OpenAI + pgvector

    Note over Patient,AI: UC-004 - Patient Uploads Clinical Documents

    Patient->>UI: Select document file(s)
    UI->>UI: Validate file type/size
    UI->>API: POST /api/documents/upload (multipart)
    API->>API: Virus scan (if configured)
    
    alt Valid document
        API->>Storage: Store original file
        Storage-->>API: File reference URI
        API->>DB: Create ClinicalDocument record
        DB-->>API: Document ID
        API->>DB: Write audit log
        
        API->>BgJob: Queue extraction job
        API-->>UI: Upload successful
        UI-->>Patient: Document uploaded, processing...
    else Invalid file
        API-->>UI: Validation error
        UI-->>Patient: Unsupported file type
    end

    Note over BgJob,AI: Background extraction pipeline
    BgJob->>DB: Update extraction_status = 'processing'
    BgJob->>Storage: Read document file
    Storage-->>BgJob: File bytes
    
    BgJob->>BgJob: Parse PDF to text
    BgJob->>BgJob: Chunk text (512 tokens, 25% overlap)
    
    loop For each chunk
        BgJob->>AI: Generate embedding
        AI-->>BgJob: Vector embedding (1536-dim)
        BgJob->>DB: Store in pgvector table
    end
    
    BgJob->>AI: Extract clinical facts (RAG)
    Note right of AI: Retrieves relevant chunks<br/>via similarity search
    AI-->>BgJob: Structured facts + confidence
    
    alt Confidence >= 70%
        BgJob->>DB: Store ExtractedFact records
        BgJob->>DB: Update extraction_status = 'completed'
        BgJob->>BgJob: Trigger 360-view update job
    else Confidence < 70%
        BgJob->>DB: Update extraction_status = 'manual_review'
        BgJob->>DB: Flag for staff review
    end
    
    BgJob->>DB: Write audit log
    
    Note over Patient,UI: Patient checks status
    Patient->>UI: Refresh document list
    UI->>API: GET /api/documents
    API->>DB: Query documents by patient
    DB-->>API: Document list with status
    API-->>UI: Documents + extraction status
    UI-->>Patient: Display processed documents
```

#### UC-005: Staff Verifies Extracted Data, Conflicts, and Suggested Codes
**Source**: [spec.md#UC-005](.propel/context/docs/spec.md#UC-005)

```mermaid
sequenceDiagram
    participant Staff
    participant UI as React Frontend
    participant API as .NET Backend API
    participant DB as PostgreSQL
    participant AI as Azure OpenAI

    Note over Staff,AI: UC-005 - Staff Verifies Extractions and Codes

    Staff->>UI: Open patient chart
    UI->>API: GET /api/patients/{id}/360-view
    API->>DB: Query PatientView360
    API->>DB: Query ExtractedFacts with sources
    API->>DB: Query conflict flags
    DB-->>API: Consolidated view + conflicts
    API-->>UI: Patient 360-degree view
    UI-->>Staff: Display aggregated data

    Note over Staff,UI: Review extracted facts
    Staff->>UI: Click on fact to see source
    UI->>API: GET /api/facts/{id}/source
    API->>DB: Query source document segment
    DB-->>API: Source text + citation
    API-->>UI: Highlight source in document
    UI-->>Staff: Show original text

    Note over Staff,UI: Resolve conflicts
    alt Conflicts detected
        UI-->>Staff: Highlight conflict items
        Staff->>UI: Review conflicting values
        UI-->>Staff: Display all source documents
        Staff->>UI: Select correct value or enter override
        UI->>API: POST /api/360-view/{id}/resolve-conflict
        API->>DB: Update consolidated_facts
        API->>DB: Clear conflict flag
        API->>DB: Write audit log with justification
        DB-->>API: Conflict resolved
        API-->>UI: Updated view
    end

    Note over Staff,AI: Review code suggestions
    Staff->>UI: View suggested ICD-10/CPT codes
    UI->>API: GET /api/patients/{id}/code-suggestions
    API->>DB: Query CodeSuggestion records
    DB-->>API: Codes + evidence links
    API-->>UI: Display codes with evidence
    UI-->>Staff: Show code suggestions

    loop For each code
        Staff->>UI: Review supporting evidence
        UI-->>Staff: Display linked facts
        Staff->>UI: Confirm or reject code
    end

    Staff->>UI: Confirm selected codes
    UI->>API: POST /api/code-suggestions/confirm
    API->>DB: Update staff_reviewed = true
    API->>DB: Set review_outcome
    API->>DB: Write audit log
    DB-->>API: Codes confirmed
    API-->>UI: Confirmation success

    Staff->>UI: Finalize patient summary
    UI->>API: PATCH /api/360-view/{id}/status
    API->>DB: Update verification_status = 'verified'
    API->>DB: Increment version (optimistic lock)
    API->>DB: Write audit log
    DB-->>API: Summary verified
    API-->>UI: Chart prep complete
    UI-->>Staff: Patient ready for visit (2 min elapsed)
```

#### UC-006: Admin Manages Users and Access
**Source**: [spec.md#UC-006](.propel/context/docs/spec.md#UC-006)

```mermaid
sequenceDiagram
    participant Admin
    participant UI as React Frontend
    participant API as .NET Backend API
    participant Identity as ASP.NET Core Identity
    participant DB as PostgreSQL
    participant Cache as Upstash Redis

    Note over Admin,Cache: UC-006 - Admin Manages Users and Access

    Admin->>UI: Access admin panel
    UI->>API: GET /api/admin/users
    API->>Identity: Verify admin role
    Identity-->>API: Authorized (admin role)
    API->>DB: Query user accounts
    DB-->>API: User list
    API-->>UI: Display users
    UI-->>Admin: Show user management interface

    Note over Admin,DB: Search for user
    Admin->>UI: Search user by name/email
    UI->>API: GET /api/admin/users/search?q={term}
    API->>DB: Query users with filter
    DB-->>API: Matching users
    API-->>UI: Filtered results
    UI-->>Admin: Display matching users

    Note over Admin,DB: Create new user
    Admin->>UI: Click "Create User"
    UI-->>Admin: Display user form
    Admin->>UI: Enter username, role, permissions
    UI->>API: POST /api/admin/users
    API->>Identity: Validate role assignment
    
    alt Valid role combination
        Identity-->>API: Role allowed
        API->>DB: Create Staff or Admin record
        API->>Identity: Create authentication credentials
        Identity-->>API: User ID
        API->>DB: Write audit log
        DB-->>API: User created
        API-->>UI: User account created
        UI-->>Admin: Success notification
    else Invalid role
        Identity-->>API: Role combination invalid
        API-->>UI: Validation error
        UI-->>Admin: Cannot assign conflicting roles
    end

    Note over Admin,DB: Update user role
    Admin->>UI: Select user, change role
    UI->>API: PATCH /api/admin/users/{id}/role
    API->>Identity: Verify admin privileges
    API->>DB: Query current user status
    DB-->>API: User is active
    
    API->>Identity: Update role claims
    Identity-->>API: Role updated
    API->>DB: Update permissions_bitfield
    API->>DB: Write audit log
    API->>Cache: Invalidate user session cache
    DB-->>API: Update successful
    API-->>UI: Role changed
    UI-->>Admin: Role updated, user must re-login

    Note over Admin,DB: Disable user account
    Admin->>UI: Select user, click "Disable"
    UI->>API: DELETE /api/admin/users/{id}
    API->>DB: Soft delete (is_active = false)
    API->>Identity: Disable authentication
    Identity-->>API: Account disabled
    API->>Cache: Terminate active sessions
    API->>DB: Write audit log
    DB-->>API: User disabled
    API-->>UI: Account disabled
    UI-->>Admin: Future logins blocked

    Note over Admin,DB: View audit trail
    Admin->>UI: View user activity history
    UI->>API: GET /api/admin/audit?userId={id}
    API->>DB: Query AuditLog by actor_id
    DB-->>API: Audit entries
    API-->>UI: Audit history
    UI-->>Admin: Display immutable audit trail
```
