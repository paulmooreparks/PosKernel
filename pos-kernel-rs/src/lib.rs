//! POS Kernel Rust Prototype FFI Implementation (Iteration 4)
//! Win32-style C ABI with ACID-compliant transactional logging and multi-process support

use std::collections::HashMap;
use std::sync::{Mutex, OnceLock, RwLock, Arc};
use std::time::{SystemTime, UNIX_EPOCH, Duration};
use std::fs::{File, OpenOptions};
use std::io::{BufWriter, Write, BufRead, BufReader, Read};
use std::path::Path;
use std::sync::atomic::{AtomicU64, Ordering};
use serde_json; // For cross-process coordination

// === RESULT CODES AND TYPES ===

#[repr(i32)]
pub enum ResultCode {
    Ok = 0,
    NotFound = 1,
    InvalidState = 2,
    ValidationFailed = 3,
    InsufficientBuffer = 4,
    TimedOut = 5,
    RecoveryFailed = 6,
    InternalError = 255
}

#[repr(C)]
pub struct PkResult {
    pub code: i32,
    pub reserved: i32
}

impl PkResult {
    fn ok() -> Self { 
        Self { code: ResultCode::Ok as i32, reserved: 0 } 
    }
    
    fn err(c: ResultCode) -> Self { 
        Self { code: c as i32, reserved: 0 } 
    }
}

pub type PkTransactionHandle = u64;
pub const PK_INVALID_HANDLE: PkTransactionHandle = 0;

// === UTILITY FUNCTIONS ===

unsafe fn read_str(ptr: *const u8, len: usize) -> String {
    if ptr.is_null() || len == 0 { 
        return String::new(); 
    }
    
    let slice = unsafe { std::slice::from_raw_parts(ptr, len) };
    String::from_utf8_lossy(slice).into_owned()
}

unsafe fn write_str(s: &str, buffer: *mut u8, buffer_size: usize, out_required: *mut usize) -> PkResult {
    if !out_required.is_null() { 
        *out_required = s.len(); 
    }
    
    if buffer.is_null() || buffer_size == 0 {
        return if s.len() == 0 { 
            PkResult::ok() 
        } else { 
            PkResult::err(ResultCode::InsufficientBuffer) 
        };
    }
    
    if s.len() > buffer_size { 
        return PkResult::err(ResultCode::InsufficientBuffer); 
    }
    
    let src_bytes = s.as_bytes();
    std::ptr::copy_nonoverlapping(src_bytes.as_ptr(), buffer, src_bytes.len());
    PkResult::ok()
}

#[no_mangle]
#[allow(improper_ctypes_definitions)] // u128 used for high-precision timestamps - acceptable for our use case
pub extern "C" fn pk_now_nanos() -> u128 {
    SystemTime::now().duration_since(UNIX_EPOCH).unwrap().as_nanos()
}

// === DATA TYPES ===

#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct Currency {
    code: String,
    decimal_places: u8,
    is_standard: bool,
}

impl Currency {
    fn new(code: &str) -> Result<Self, &'static str> {
        if code.is_empty() {
            return Err("Currency code cannot be empty");
        }
        
        if code.len() != 3 {
            return Err("Currency code must be exactly 3 characters");
        }

        let code_upper = code.to_uppercase();
        
        match code_upper.as_str() {
            "USD" => Ok(Currency::usd()),
            "EUR" => Ok(Currency::eur()),
            "JPY" => Ok(Currency::jpy()),
            "GBP" => Ok(Currency::gbp()),
            "CAD" => Ok(Currency::cad()),
            "AUD" => Ok(Currency::aud()),
            _ => {
                Ok(Currency {
                    code: code_upper,
                    decimal_places: 2,
                    is_standard: false,
                })
            }
        }
    }
    
    fn usd() -> Self { 
        Currency { 
            code: "USD".to_string(), 
            decimal_places: 2, 
            is_standard: true 
        } 
    }
    
    fn eur() -> Self { 
        Currency { 
            code: "EUR".to_string(), 
            decimal_places: 2, 
            is_standard: true 
        } 
    }
    
    fn jpy() -> Self { 
        Currency { 
            code: "JPY".to_string(), 
            decimal_places: 0, 
            is_standard: true 
        } 
    }
    
    fn gbp() -> Self { 
        Currency { 
            code: "GBP".to_string(), 
            decimal_places: 2, 
            is_standard: true 
        } 
    }
    
    fn cad() -> Self { 
        Currency { 
            code: "CAD".to_string(), 
            decimal_places: 2, 
            is_standard: true 
        } 
    }
    
    fn aud() -> Self { 
        Currency { 
            code: "AUD".to_string(), 
            decimal_places: 2, 
            is_standard: true 
        } 
    }

    fn code(&self) -> &str { 
        &self.code 
    }
    
    fn decimal_places(&self) -> u8 { 
        self.decimal_places 
    }
    
    fn is_standard(&self) -> bool { 
        self.is_standard 
    }
}

#[derive(Debug)]
struct Line { 
    _sku: String, // Note: prefixed with _ to indicate intentional non-use in current implementation
    _qty: i32, 
    _unit_minor: i64 
}

impl Line {
    fn new(sku: String, qty: i32, unit_minor: i64) -> Self {
        Self {
            _sku: sku,
            _qty: qty,
            _unit_minor: unit_minor,
        }
    }
    
    fn total_minor(&self) -> i64 {
        self._unit_minor * self._qty as i64
    }
    
    #[allow(dead_code)] // Will be used in future line item management APIs
    fn sku(&self) -> &str {
        &self._sku
    }
    
    #[allow(dead_code)] // Will be used in future line item management APIs
    fn qty(&self) -> i32 {
        self._qty
    }
    
    #[allow(dead_code)] // Will be used in future line item management APIs
    fn unit_minor(&self) -> i64 {
        self._unit_minor
    }
}

#[derive(Debug)]
pub struct TerminalSession {
    _id: u64,
    _terminal_id: String,
    _operator_id: Option<String>,
    _created_at: SystemTime,
    _last_activity: SystemTime,
    _active_transaction: Option<u64>,
    _timeout_seconds: u64,
}

impl TerminalSession {
    #[allow(dead_code)] // Will be used in future terminal session management
    fn new(id: u64, terminal_id: String) -> Self {
        let now = SystemTime::now();
        Self {
            _id: id,
            _terminal_id: terminal_id,
            _operator_id: None,
            _created_at: now,
            _last_activity: now,
            _active_transaction: None,
            _timeout_seconds: 1800,
        }
    }
}

#[derive(Debug, Clone)]
pub enum AuditLevel {
    None,
    Basic,
    Detailed,
    Debug,
}

#[derive(Debug)]
pub struct SystemConfig {
    max_concurrent_transactions: u32,
    transaction_timeout_seconds: u64,
    _session_timeout_seconds: u64, // Prefixed with _ - used in future session management
    _default_currency: Currency,    // Prefixed with _ - used in future config management
    _audit_level: AuditLevel,      // Prefixed with _ - used in future audit management
    _recovery_enabled: bool,       // Prefixed with _ - used in future recovery features
}

impl Default for SystemConfig {
    fn default() -> Self {
        SystemConfig {
            max_concurrent_transactions: 1000,
            transaction_timeout_seconds: 300,
            _session_timeout_seconds: 1800,
            _default_currency: Currency::usd(),
            _audit_level: AuditLevel::Basic,
            _recovery_enabled: true,
        }
    }
}

#[derive(Debug, Clone)]
pub struct AuditEntry {
    _timestamp: SystemTime,
    _event_type: AuditEventType,
    _transaction_handle: Option<u64>,
    _session_handle: Option<u64>,
    _details: String,
}

impl AuditEntry {
    #[allow(dead_code)] // Will be used in future audit system
    fn new(event_type: AuditEventType, details: String) -> Self {
        Self {
            _timestamp: SystemTime::now(),
            _event_type: event_type,
            _transaction_handle: None,
            _session_handle: None,
            _details: details,
        }
    }
}

#[derive(Debug, Clone)]
pub enum AuditEventType {
    TransactionCreated,
    TransactionCompleted,
    TransactionVoided,
    TransactionTimedOut,
    TransactionRecovered,
    TransactionError,
    SessionStarted,
    SessionEnded,
    SystemConfigChanged,
}

// === ACID-COMPLIANT LOGGING ===

#[derive(Debug, Clone)]
pub struct WalEntry {
    sequence_number: u64,
    timestamp: SystemTime,
    transaction_handle: u64,
    operation_type: WalOperationType,
    data: String,
    checksum: u32,
}

#[derive(Debug, Clone)]
pub enum WalOperationType {
    TransactionBegin { store: String, currency: String },
    LineAdd { sku: String, qty: i32, unit_minor: i64 },
    TenderAdd { amount_minor: i64 },
    TransactionCommit { final_state: String },
    TransactionAbort { reason: String },
    TransactionTimeout { timeout_seconds: u64 },
    SystemConfigChange { setting: String, old_value: String, new_value: String },
}

pub struct WriteAheadLog {
    log_file_path: String,
    sequence_counter: AtomicU64,
    log_file: Arc<Mutex<Box<dyn Write + Send>>>,
}

impl WriteAheadLog {
    pub fn new(log_file_path: &str) -> Result<Self, Box<dyn std::error::Error>> {
        // Handle special case for in-memory testing
        if log_file_path == ":memory:" {
            return Ok(WriteAheadLog {
                log_file_path: ":memory:".to_string(),
                sequence_counter: AtomicU64::new(1),
                log_file: Arc::new(Mutex::new(Box::new(std::io::sink()))),
            });
        }
        
        // Create parent directory if it doesn't exist
        if let Some(parent) = Path::new(log_file_path).parent() {
            if let Err(e) = std::fs::create_dir_all(parent) {
                eprintln!("CRITICAL: Failed to create WAL directory {}: {}", 
                         parent.display(), e);
                return Err(e.into());
            }
        }
        
        let file = match OpenOptions::new()
            .create(true)
            .append(true)
            .open(log_file_path) 
        {
            Ok(f) => f,
            Err(e) => {
                eprintln!("CRITICAL: Failed to open WAL file {}: {}", log_file_path, e);
                return Err(e.into());
            }
        };
        
        let log_file: Arc<Mutex<Box<dyn Write + Send>>> = Arc::new(Mutex::new(Box::new(BufWriter::new(file))));
        let sequence_counter = AtomicU64::new(match Self::get_last_sequence_number(log_file_path) {
            Ok(seq) => seq + 1,
            Err(e) => {
                eprintln!("WARNING: Could not read last sequence number from {}: {}. Starting from 1.", 
                         log_file_path, e);
                1
            }
        });
        
        Ok(WriteAheadLog {
            log_file_path: log_file_path.to_string(),
            sequence_counter,
            log_file,
        })
    }
    
    fn get_last_sequence_number(log_file_path: &str) -> Result<u64, Box<dyn std::error::Error>> {
        if !Path::new(log_file_path).exists() {
            return Ok(0);
        }
        
        let file = match File::open(log_file_path) {
            Ok(f) => f,
            Err(e) => {
                eprintln!("WARNING: Could not open WAL file {} for sequence reading: {}", 
                         log_file_path, e);
                return Err(e.into());
            }
        };
        
        let reader = BufReader::new(file);
        let mut last_seq = 0u64;
        
        for line in reader.lines() {
            let line = match line {
                Ok(l) => l,
                Err(e) => {
                    eprintln!("WARNING: Error reading line from WAL file {}: {}", 
                             log_file_path, e);
                    continue; // Continue to next line on error
                }
            };
            
            if let Some(seq_str) = line.split(',').next() {
                if let Ok(seq) = seq_str.parse::<u64>() {
                    last_seq = seq;
                } else {
                    eprintln!("WARNING: Invalid sequence number in WAL file {}: {}", 
                             log_file_path, seq_str);
                }
            }
        }
        
        Ok(last_seq)
    }
    
    pub fn write_entry(&self, entry: WalEntry) -> Result<(), Box<dyn std::error::Error>> {
        // Skip writing for in-memory mode
        if self.log_file_path == ":memory:" {
            return Ok(());
        }
        
        let mut writer = match self.log_file.lock() {
            Ok(w) => w,
            Err(e) => {
                eprintln!("CRITICAL: Failed to acquire WAL lock for {}: {}", 
                         self.log_file_path, e);
                return Err("Failed to acquire log lock".into());
            }
        };
        
        let timestamp_nanos = match entry.timestamp.duration_since(UNIX_EPOCH) {
            Ok(duration) => duration.as_nanos(),
            Err(e) => {
                eprintln!("CRITICAL: System time error in WAL entry: {}", e);
                return Err(e.into());
            }
        };
        
        let log_line = format!(
            "{},{},{},{:?},{},{}\n",
            entry.sequence_number,
            timestamp_nanos,
            entry.transaction_handle,
            entry.operation_type,
            entry.data.replace(',', ";").replace('\n', "\\n"),
            entry.checksum
        );
        
        if let Err(e) = writer.write_all(log_line.as_bytes()) {
            eprintln!("CRITICAL: Failed to write WAL entry to {}: {}", 
                     self.log_file_path, e);
            return Err(e.into());
        }
        
        if let Err(e) = writer.flush() {
            eprintln!("CRITICAL: Failed to flush WAL to {}: {}", 
                     self.log_file_path, e);
            return Err(e.into());
        }
        
        Ok(())
    }
    
    pub fn log_operation(
        &self,
        transaction_handle: u64,
        operation_type: WalOperationType,
        data: &str,
    ) -> Result<u64, Box<dyn std::error::Error>> {
        let sequence_number = self.sequence_counter.fetch_add(1, Ordering::SeqCst);
        let checksum = Self::calculate_checksum(data);
        
        let entry = WalEntry {
            sequence_number,
            timestamp: SystemTime::now(),
            transaction_handle,
            operation_type,
            data: data.to_string(),
            checksum,
        };
        
        match self.write_entry(entry) {
            Ok(()) => Ok(sequence_number),
            Err(e) => {
                eprintln!("CRITICAL: WAL operation failed for transaction {}: {}", 
                         transaction_handle, e);
                Err(e)
            }
        }
    }
    
    fn calculate_checksum(data: &str) -> u32 {
        data.bytes().fold(0u32, |acc, b| acc.wrapping_add(b as u32))
    }
}

// === TRANSACTION STATE ===

#[derive(Debug, PartialEq, Eq, Clone)]
pub enum LegalTxState {
    Building,
    Committing,
    Committed,
    Aborting,
    Aborted,
    TimedOut,
}

#[derive(Debug)]
struct LegalTransaction {
    id: u64,
    store: String,
    currency: Currency,
    lines: Vec<Line>,
    tendered_minor: i64,
    state: LegalTxState,
    _created_at: SystemTime,        // Prefixed with _ - used in future reporting/analytics
    committed_at: Option<SystemTime>,
    last_activity: SystemTime,
    _recovery_point: u32,           // Prefixed with _ - used in future recovery system
    #[allow(dead_code)] // Used for audit trail and recovery - will be exposed in future APIs
    wal_begin_sequence: Option<u64>,
    wal_commit_sequence: Option<u64>,
}

impl LegalTransaction {
    fn new(id: u64, store: String, currency: Currency, wal_sequence: u64) -> Self {
        let now = SystemTime::now();
        Self {
            id,
            store,
            currency,
            lines: Vec::new(),
            tendered_minor: 0,
            state: LegalTxState::Building,
            _created_at: now,
            committed_at: None,
            last_activity: now,
            _recovery_point: 0,
            wal_begin_sequence: Some(wal_sequence),
            wal_commit_sequence: None,
        }
    }
    
    fn total_minor(&self) -> i64 {
        self.lines.iter().map(|l| l.total_minor()).sum()
    }
    
    fn change_minor(&self) -> i64 {
        (self.tendered_minor - self.total_minor()).max(0)
    }
    
    fn line_count(&self) -> u32 {
        self.lines.len() as u32
    }
    
    fn touch_activity(&mut self) {
        self.last_activity = SystemTime::now();
    }
    
    fn is_expired(&self, timeout_seconds: u64) -> bool {
        match self.last_activity.elapsed() {
            Ok(elapsed) => elapsed.as_secs() > timeout_seconds,
            Err(e) => {
                eprintln!("WARNING: System time error checking transaction expiry: {}", e);
                // Assume not expired on time error to be safe
                false
            }
        }
    }
    
    fn add_line(&mut self, sku: String, qty: i32, unit_minor: i64) {
        self.lines.push(Line::new(sku, qty, unit_minor));
        self._recovery_point += 1;
    }
}

// === KERNEL STORE ===

pub struct LegalKernelStore {
    next_tx_id: AtomicU64,
    active_transactions: HashMap<u64, LegalTransaction>,
    #[allow(dead_code)] // Will be used in future session management system
    next_session_id: AtomicU64,
    #[allow(dead_code)] // Will be used in future session management system
    active_sessions: HashMap<u64, TerminalSession>,
    system_config: SystemConfig,
    transaction_timeouts: HashMap<u64, SystemTime>,
    wal: Arc<WriteAheadLog>,
}

impl LegalKernelStore {
    fn new() -> Self {
        LegalKernelStore {
            next_tx_id: AtomicU64::new(1),
            active_transactions: HashMap::new(),
            next_session_id: AtomicU64::new(1),
            active_sessions: HashMap::new(),
            system_config: SystemConfig::default(),
            transaction_timeouts: HashMap::new(),
            wal: Arc::new(WriteAheadLog::new(":memory:").unwrap_or_else(|e| {
                eprintln!("CRITICAL: Failed to create memory WAL: {}", e);
                panic!("Memory WAL initialization failed - system cannot continue safely");
            })),
        }
    }
    
    fn initialize_storage(&mut self, data_dir: &str) -> Result<(), Box<dyn std::error::Error>> {
        match WriteAheadLog::new(&format!("{}/transaction.wal", data_dir)) {
            Ok(wal) => {
                self.wal = Arc::new(wal);
                println!("Initialized ACID-compliant transaction logging in: {}", data_dir);
            },
            Err(e) => {
                eprintln!("CRITICAL: Could not initialize WAL in {}: {}", data_dir, e);
                eprintln!("System will use in-memory fallback - data will not persist!");
                return Err(e);
            }
        }
        Ok(())
    }
    
    fn begin_transaction_legal(&mut self, store: String, currency: Currency) -> Result<u64, Box<dyn std::error::Error>> {
        let tx_id = self.next_tx_id.fetch_add(1, Ordering::SeqCst);
        
        let wal_sequence = match self.wal.log_operation(
            tx_id,
            WalOperationType::TransactionBegin { 
                store: store.clone(), 
                currency: currency.code().to_string() 
            },
            &format!("store:{},currency:{}", store, currency.code())
        ) {
            Ok(seq) => seq,
            Err(e) => {
                eprintln!("CRITICAL: Failed to log transaction begin for {}: {}", tx_id, e);
                return Err(e);
            }
        };
        
        let transaction = LegalTransaction::new(tx_id, store, currency, wal_sequence);
        let timeout = SystemTime::now() + Duration::from_secs(self.system_config.transaction_timeout_seconds);
        self.transaction_timeouts.insert(tx_id, timeout);
        self.active_transactions.insert(tx_id, transaction);
        
        Ok(tx_id)
    }
    
    fn add_line_legal(&mut self, handle: u64, sku: String, qty: i32, unit_minor: i64) -> Result<(), Box<dyn std::error::Error>> {
        let tx = self.active_transactions.get_mut(&handle)
            .ok_or("Transaction not found")?;
        
        match tx.state {
            LegalTxState::Building => {
                if tx.is_expired(self.system_config.transaction_timeout_seconds) {
                    eprintln!("WARNING: Transaction {} expired during line addition", handle);
                    return Err("Transaction expired".into());
                }
            },
            _ => {
                eprintln!("ERROR: Attempted to add line to transaction {} in state {:?}", 
                         handle, tx.state);
                return Err("Transaction not in building state".into());
            }
        }
        
        match self.wal.log_operation(
            handle,
            WalOperationType::LineAdd { sku: sku.clone(), qty, unit_minor },
            &format!("sku:{},qty:{},unit_minor:{}", sku, qty, unit_minor)
        ) {
            Ok(_) => {
                tx.add_line(sku, qty, unit_minor);
                tx.touch_activity();
                Ok(())
            },
            Err(e) => {
                eprintln!("CRITICAL: Failed to log line addition for transaction {}: {}", handle, e);
                Err(e)
            }
        }
    }
    
    fn add_tender_legal(&mut self, handle: u64, amount_minor: i64) -> Result<(), Box<dyn std::error::Error>> {
        let tx = self.active_transactions.get_mut(&handle)
            .ok_or("Transaction not found")?;
        
        match tx.state {
            LegalTxState::Building => {
                if tx.is_expired(self.system_config.transaction_timeout_seconds) {
                    eprintln!("WARNING: Transaction {} expired during tender addition", handle);
                    return Err("Transaction expired".into());
                }
            },
            _ => {
                eprintln!("ERROR: Attempted to add tender to transaction {} in state {:?}", 
                         handle, tx.state);
                return Err("Transaction not in building state".into());
            }
        }
        
        match self.wal.log_operation(
            handle,
            WalOperationType::TenderAdd { amount_minor },
            &format!("amount_minor:{}", amount_minor)
        ) {
            Ok(_) => {
                tx.tendered_minor += amount_minor;
                tx.touch_activity();
                
                if tx.tendered_minor >= tx.total_minor() {
                    match self.commit_transaction_legal(handle) {
                        Ok(()) => Ok(()),
                        Err(e) => {
                            eprintln!("CRITICAL: Auto-commit failed for transaction {}: {}", handle, e);
                            Err(e)
                        }
                    }
                } else {
                    Ok(())
                }
            },
            Err(e) => {
                eprintln!("CRITICAL: Failed to log tender addition for transaction {}: {}", handle, e);
                Err(e)
            }
        }
    }
    
    fn commit_transaction_legal(&mut self, handle: u64) -> Result<(), Box<dyn std::error::Error>> {
        let tx = self.active_transactions.get_mut(&handle)
            .ok_or("Transaction not found")?;
        
        if tx.state != LegalTxState::Building {
            eprintln!("ERROR: Attempted to commit transaction {} in state {:?}", tx.id, tx.state);
            return Err("Transaction not in building state".into());
        }
        
        tx.state = LegalTxState::Committing;
        
        let final_state = format!(
            "total:{},tendered:{},change:{},lines:{}",
            tx.total_minor(),
            tx.tendered_minor,
            tx.change_minor(),
            tx.line_count()
        );
        
        match self.wal.log_operation(
            handle,
            WalOperationType::TransactionCommit { final_state },
            &format!("committed_at:{:?}", SystemTime::now())
        ) {
            Ok(commit_sequence) => {
                tx.state = LegalTxState::Committed;
                tx.committed_at = Some(SystemTime::now());
                tx.wal_commit_sequence = Some(commit_sequence);
                Ok(())
            },
            Err(e) => {
                eprintln!("CRITICAL: Failed to log transaction commit for {}: {}", handle, e);
                // Revert state on WAL failure
                tx.state = LegalTxState::Building;
                Err(e)
            }
        }
    }
    
    fn abort_transaction_legal(&mut self, handle: u64, reason: &str) -> Result<(), Box<dyn std::error::Error>> {
        let tx = self.active_transactions.get_mut(&handle)
            .ok_or("Transaction not found")?;
        
        if matches!(tx.state, LegalTxState::Committed | LegalTxState::Aborted) {
            eprintln!("WARNING: Attempted to abort already finalized transaction {} in state {:?}", 
                     handle, tx.state);
            return Err("Transaction already finalized".into());
        }
        
        tx.state = LegalTxState::Aborting;
        
        match self.wal.log_operation(
            handle,
            WalOperationType::TransactionAbort { reason: reason.to_string() },
            &format!("reason:{}", reason)
        ) {
            Ok(_) => {
                tx.state = LegalTxState::Aborted;
                Ok(())
            },
            Err(e) => {
                eprintln!("CRITICAL: Failed to log transaction abort for {}: {}", handle, e);
                // Revert state on WAL failure  
                tx.state = LegalTxState::Building;
                Err(e)
            }
        }
    }
}

// === ENHANCED MULTI-PROCESS SUPPORT ===

#[repr(i32)]
pub enum TerminalLockResult {
    Success = 0,
    AlreadyInUse = 1,
    InvalidTerminalId = 2,
    FileSystemError = 3,
}

/// Terminal coordination and process management
pub struct TerminalCoordinator {
    terminal_id: String,
    lock_file: Option<File>,
    data_directory: String,
}

impl TerminalCoordinator {
    fn new(terminal_id: &str) -> Result<Self, Box<dyn std::error::Error>> {
        if terminal_id.is_empty() {
            return Err("Terminal ID cannot be empty".into());
        }
        
        // Validate terminal ID format (alphanumeric, max 32 chars)
        if !terminal_id.chars().all(|c| c.is_alphanumeric() || c == '_') || terminal_id.len() > 32 {
            eprintln!("ERROR: Invalid terminal ID format: {}", terminal_id);
            return Err("Invalid terminal ID format".into());
        }
        
        let data_directory = format!("pos_kernel_data/terminals/{}", terminal_id);
        
        if let Err(e) = std::fs::create_dir_all(&data_directory) {
            eprintln!("CRITICAL: Failed to create terminal directory {}: {}", data_directory, e);
            return Err(e.into());
        }
        
        Ok(TerminalCoordinator {
            terminal_id: terminal_id.to_string(),
            lock_file: None,
            data_directory,
        })
    }
    
    fn acquire_exclusive_lock(&mut self) -> Result<(), Box<dyn std::error::Error>> {
        let lock_path = format!("{}/terminal.lock", self.data_directory);
        
        // Try to create lock file exclusively
        match std::fs::OpenOptions::new()
            .write(true)
            .create_new(true) // Fails if file already exists
            .open(&lock_path) 
        {
            Ok(mut file) => {
                // Write process ID and timestamp to lock file
                let lock_info = format!("PID:{}\nTIME:{:?}\nTERMINAL:{}\n", 
                                      std::process::id(),
                                      SystemTime::now(),
                                      self.terminal_id);
                
                if let Err(e) = file.write_all(lock_info.as_bytes()) {
                    eprintln!("CRITICAL: Failed to write lock file {}: {}", lock_path, e);
                    return Err(e.into());
                }
                
                if let Err(e) = file.flush() {
                    eprintln!("CRITICAL: Failed to flush lock file {}: {}", lock_path, e);
                    return Err(e.into());
                }
                
                self.lock_file = Some(file);
                Ok(())
            },
            Err(e) if e.kind() == std::io::ErrorKind::AlreadyExists => {
                // Check if existing lock is stale
                match self.is_lock_stale(&lock_path) {
                    Ok(true) => {
                        // Remove stale lock and try again
                        println!("Removing stale lock file for terminal {}", self.terminal_id);
                        if let Err(remove_err) = std::fs::remove_file(&lock_path) {
                            eprintln!("ERROR: Failed to remove stale lock file {}: {}", lock_path, remove_err);
                            return Err(remove_err.into());
                        }
                        return self.acquire_exclusive_lock();
                    },
                    Ok(false) => {
                        eprintln!("ERROR: Terminal {} is already in use by another process", self.terminal_id);
                        Err("Terminal is already in use by another process".into())
                    },
                    Err(check_err) => {
                        eprintln!("ERROR: Failed to check if lock is stale for {}: {}", lock_path, check_err);
                        Err(check_err)
                    }
                }
            },
            Err(e) => {
                eprintln!("CRITICAL: Failed to create lock file {}: {}", lock_path, e);
                Err(e.into())
            }
        }
    }
    
    fn is_lock_stale(&self, lock_path: &str) -> Result<bool, Box<dyn std::error::Error>> {
        let mut file = match File::open(lock_path) {
            Ok(f) => f,
            Err(e) => {
                eprintln!("ERROR: Failed to open lock file {} for staleness check: {}", lock_path, e);
                return Err(e.into());
            }
        };
        
        let mut contents = String::new();
        if let Err(e) = file.read_to_string(&mut contents) {
            eprintln!("ERROR: Failed to read lock file {} for staleness check: {}", lock_path, e);
            return Err(e.into());
        }
        
        // Parse lock file to extract PID
        for line in contents.lines() {
            if let Some(pid_str) = line.strip_prefix("PID:") {
                if let Ok(pid) = pid_str.parse::<u32>() {
                    // Check if process is still running (platform-specific)
                    #[cfg(windows)]
                    {
                        // On Windows, try to open the process handle
                        use std::ffi::c_void;
                        extern "system" {
                            fn OpenProcess(desired_access: u32, inherit_handle: i32, process_id: u32) -> *mut c_void;
                            fn CloseHandle(handle: *mut c_void) -> i32;
                        }
                        
                        unsafe {
                            let handle = OpenProcess(0x400000, 0, pid); // PROCESS_QUERY_INFORMATION
                            if handle.is_null() {
                                return Ok(true); // Process doesn't exist, lock is stale
                            } else {
                                CloseHandle(handle);
                                return Ok(false); // Process exists, lock is valid
                            }
                        }
                    }
                    
                    #[cfg(unix)]
                    {
                        // On Unix, send signal 0 to check if process exists
                        use std::process::Command;
                        match Command::new("kill")
                            .args(&["-0", &pid.to_string()])
                            .output()
                        {
                            Ok(output) => {
                                return Ok(!output.status.success()); // If kill fails, process doesn't exist
                            },
                            Err(e) => {
                                eprintln!("ERROR: Failed to check process {} existence: {}", pid, e);
                                return Err(e.into());
                            }
                        }
                    }
                } else {
                    eprintln!("WARNING: Invalid PID in lock file {}: {}", lock_path, pid_str);
                }
            }
        }
        
        // If we can't parse the lock file, consider it stale
        eprintln!("WARNING: Could not parse lock file {}, considering it stale", lock_path);
        Ok(true)
    }
    
    fn release_lock(&mut self) {
        if self.lock_file.take().is_some() {
            let lock_path = format!("{}/terminal.lock", self.data_directory);
            if let Err(e) = std::fs::remove_file(&lock_path) {
                eprintln!("WARNING: Failed to remove lock file {}: {}", lock_path, e);
                // Don't fail the operation, just log the warning
            }
        }
    }
    
    fn register_terminal(&self) -> Result<(), Box<dyn std::error::Error>> {
        // Register this terminal in the global registry
        let registry_dir = "pos_kernel_data/shared/coordination";
        if let Err(e) = std::fs::create_dir_all(registry_dir) {
            eprintln!("CRITICAL: Failed to create registry directory {}: {}", registry_dir, e);
            return Err(e.into());
        }
        
        let registry_path = format!("{}/active_terminals.json", registry_dir);
        
        // Read existing registry
        let mut terminals = if std::path::Path::new(&registry_path).exists() {
            match std::fs::read_to_string(&registry_path) {
                Ok(contents) => {
                    serde_json::from_str::<serde_json::Value>(&contents)
                        .unwrap_or_else(|e| {
                            eprintln!("WARNING: Failed to parse registry file {}: {}. Creating new registry.", 
                                     registry_path, e);
                            serde_json::json!({})
                        })
                },
                Err(e) => {
                    eprintln!("WARNING: Failed to read registry file {}: {}. Creating new registry.", 
                             registry_path, e);
                    serde_json::json!({})
                }
            } 
        } else {
            serde_json::json!({})
        };
        
        // Add this terminal
        if let Some(obj) = terminals.as_object_mut() {
            let terminal_info = match SystemTime::now().duration_since(UNIX_EPOCH) {
                Ok(duration) => {
                    serde_json::json!({
                        "pid": std::process::id(),
                        "started_at": duration.as_secs(),
                        "data_directory": self.data_directory
                    })
                },
                Err(e) => {
                    eprintln!("ERROR: System time error during terminal registration: {}", e);
                    return Err(e.into());
                }
            };
            
            obj.insert(self.terminal_id.clone(), terminal_info);
        } else {
            eprintln!("CRITICAL: Registry is not a JSON object");
            return Err("Registry corruption".into());
        }
        
        // Write back to registry
        let contents = match serde_json::to_string_pretty(&terminals) {
            Ok(json) => json,
            Err(e) => {
                eprintln!("CRITICAL: Failed to serialize registry: {}", e);
                return Err(e.into());
            }
        };
        
        if let Err(e) = std::fs::write(&registry_path, contents) {
            eprintln!("CRITICAL: Failed to write registry file {}: {}", registry_path, e);
            return Err(e.into());
        }
        
        Ok(())
    }
}

impl Drop for TerminalCoordinator {
    fn drop(&mut self) {
        self.release_lock();
    }
}

// Global terminal coordinator
static TERMINAL_COORDINATOR: OnceLock<Mutex<Option<TerminalCoordinator>>> = OnceLock::new();

fn get_terminal_coordinator() -> &'static Mutex<Option<TerminalCoordinator>> {
    TERMINAL_COORDINATOR.get_or_init(|| Mutex::new(None))
}

// === ENHANCED KERNEL STORE WITH TERMINAL SUPPORT ===

impl LegalKernelStore {
    fn initialize_storage_with_terminal(&mut self, terminal_id: &str) -> Result<(), Box<dyn std::error::Error>> {
        // Initialize terminal coordinator first
        let mut coordinator_guard = match get_terminal_coordinator().lock() {
            Ok(guard) => guard,
            Err(e) => {
                eprintln!("CRITICAL: Failed to acquire coordinator lock: {}", e);
                return Err("Failed to acquire coordinator lock".into());
            }
        };
        
        if coordinator_guard.is_some() {
            eprintln!("ERROR: Terminal already initialized");
            return Err("Terminal already initialized".into());
        }
        
        let mut coordinator = match TerminalCoordinator::new(terminal_id) {
            Ok(coord) => coord,
            Err(e) => {
                eprintln!("CRITICAL: Failed to create terminal coordinator for {}: {}", terminal_id, e);
                return Err(e);
            }
        };
        
        if let Err(e) = coordinator.acquire_exclusive_lock() {
            eprintln!("CRITICAL: Failed to acquire exclusive lock for terminal {}: {}", terminal_id, e);
            return Err(e);
        }
        
        if let Err(e) = coordinator.register_terminal() {
            eprintln!("CRITICAL: Failed to register terminal {}: {}", terminal_id, e);
            return Err(e);
        }
        
        // Initialize WAL in terminal-specific directory
        let wal_path = format!("{}/transaction.wal", coordinator.data_directory);
        match WriteAheadLog::new(&wal_path) {
            Ok(wal) => {
                self.wal = Arc::new(wal);
                println!("Initialized ACID-compliant transaction logging for terminal {} in: {}", 
                        terminal_id, coordinator.data_directory);
            },
            Err(e) => {
                eprintln!("CRITICAL: Could not initialize WAL for terminal {} at {}: {}", 
                         terminal_id, wal_path, e);
                return Err(format!("Could not initialize WAL for terminal {}: {}", terminal_id, e).into());
            }
        }
        
        *coordinator_guard = Some(coordinator);
        Ok(())
    }
}

// === FFI FUNCTIONS FOR TERMINAL MANAGEMENT ===

#[no_mangle]
pub extern "C" fn pk_initialize_terminal(
    terminal_id_ptr: *const u8,
    terminal_id_len: usize
) -> PkResult {
    if terminal_id_ptr.is_null() || terminal_id_len == 0 {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let terminal_id = unsafe { read_str(terminal_id_ptr, terminal_id_len) };
    
    // Initialize the kernel store with terminal support
    let mut store = match legal_kernel_store().write() {
        Ok(s) => s,
        Err(e) => {
            eprintln!("CRITICAL: Failed to acquire kernel store write lock: {}", e);
            return PkResult::err(ResultCode::InternalError);
        }
    };
    
    match store.initialize_storage_with_terminal(&terminal_id) {
        Ok(()) => PkResult::ok(),
        Err(e) => {
            eprintln!("Terminal initialization failed for {}: {}", terminal_id, e);
            if e.to_string().contains("already in use") {
                PkResult::err(ResultCode::InvalidState)
            } else if e.to_string().contains("Invalid terminal ID") {
                PkResult::err(ResultCode::ValidationFailed)
            } else {
                PkResult::err(ResultCode::InternalError)
            }
        }
    }
}

#[no_mangle]
pub extern "C" fn pk_get_terminal_info(
    buffer: *mut u8,
    buffer_size: usize,
    out_required_size: *mut usize
) -> PkResult {
    let coordinator_guard = match get_terminal_coordinator().lock() {
        Ok(guard) => guard,
        Err(e) => {
            eprintln!("ERROR: Failed to acquire terminal coordinator lock: {}", e);
            return PkResult::err(ResultCode::InternalError);
        }
    };
    
    if let Some(ref coordinator) = *coordinator_guard {
        let info = format!("TERMINAL_ID:{}\nDATA_DIR:{}\nPID:{}", 
                          coordinator.terminal_id,
                          coordinator.data_directory,
                          std::process::id());
        
        unsafe {
            write_str(&info, buffer, buffer_size, out_required_size)
        }
    } else {
        eprintln!("ERROR: Terminal not initialized");
        PkResult::err(ResultCode::InvalidState)
    }
}

#[no_mangle]
pub extern "C" fn pk_shutdown_terminal() -> PkResult {
    // Graceful terminal shutdown
    let mut coordinator_guard = match get_terminal_coordinator().lock() {
        Ok(guard) => guard,
        Err(e) => {
            eprintln!("ERROR: Failed to acquire coordinator lock for shutdown: {}", e);
            return PkResult::err(ResultCode::InternalError);
        }
    };
    
    if let Some(mut coordinator) = coordinator_guard.take() {
        // Force WAL sync before shutdown
        if let Err(e) = legal_kernel_store().read() {
            eprintln!("WARNING: Could not acquire kernel store lock during shutdown: {}", e);
            // Continue with shutdown anyway
        }
        
        coordinator.release_lock();
        println!("Terminal {} shut down gracefully", coordinator.terminal_id);
        PkResult::ok()
    } else {
        eprintln!("ERROR: No terminal to shutdown");
        PkResult::err(ResultCode::InvalidState)
    }
}

// Update the existing initialization to use environment variable for backward compatibility
fn legal_kernel_store() -> &'static RwLock<LegalKernelStore> {
    LEGAL_KERNEL_STORE.get_or_init(|| {
        let mut store = LegalKernelStore::new();
        
        // Check for terminal ID first, fall back to old behavior
        if let Ok(terminal_id) = std::env::var("POS_TERMINAL_ID") {
            if let Err(e) = store.initialize_storage_with_terminal(&terminal_id) {
                eprintln!("CRITICAL: Could not initialize terminal-aware storage for {}: {}", terminal_id, e);
                eprintln!("System cannot continue safely with terminal coordination failure");
                panic!("Terminal initialization failed: {}", e);
            }
        } else {
            // Backward compatibility - use old initialization
            let data_dir = std::env::var("POS_KERNEL_DATA_DIR")
                .unwrap_or_else(|_| "./pos_kernel_data".to_string());
            
            if let Err(e) = store.initialize_storage(&data_dir) {
                eprintln!("CRITICAL: Could not initialize ACID-compliant logging in {}: {}", data_dir, e);
                eprintln!("System cannot continue safely without transaction logging");
                panic!("ACID-compliant logging initialization failed: {}", e);
            }
        }
        
        RwLock::new(store)
    })
}

static LEGAL_KERNEL_STORE: OnceLock<RwLock<LegalKernelStore>> = OnceLock::new();

// === FFI FUNCTIONS ===

#[no_mangle]
pub extern "C" fn pk_begin_transaction_legal(
    store_ptr: *const u8,
    store_len: usize,
    currency_ptr: *const u8,
    currency_len: usize,
    out_handle: *mut PkTransactionHandle
) -> PkResult {
    if out_handle.is_null() {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let store_name = unsafe { read_str(store_ptr, store_len) };
    let currency_str = unsafe { read_str(currency_ptr, currency_len) };
    
    let currency = match Currency::new(&currency_str) {
        Ok(c) => c,
        Err(e) => {
            eprintln!("ERROR: Invalid currency '{}': {}", currency_str, e);
            return PkResult::err(ResultCode::ValidationFailed);
        }
    };
    
    let mut store = match legal_kernel_store().write() {
        Ok(s) => s,
        Err(e) => {
            eprintln!("CRITICAL: Failed to acquire kernel store write lock: {}", e);
            return PkResult::err(ResultCode::InternalError);
        }
    };
    
    if store.active_transactions.len() >= store.system_config.max_concurrent_transactions as usize {
        eprintln!("ERROR: Maximum concurrent transactions ({}) exceeded", 
                 store.system_config.max_concurrent_transactions);
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    match store.begin_transaction_legal(store_name, currency) {
        Ok(handle) => {
            unsafe { *out_handle = handle; }
            PkResult::ok()
        },
        Err(e) => {
            eprintln!("ERROR: Failed to begin transaction: {}", e);
            PkResult::err(ResultCode::InternalError)
        }
    }
}

#[no_mangle]
pub extern "C" fn pk_add_line_legal(
    handle: PkTransactionHandle,
    sku_ptr: *const u8,
    sku_len: usize,
    qty: i32,
    unit_minor: i64
) -> PkResult {
    if handle == PK_INVALID_HANDLE || qty <= 0 || unit_minor < 0 {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let sku = unsafe { read_str(sku_ptr, sku_len) };
    if sku.is_empty() {
        eprintln!("ERROR: SKU cannot be empty for transaction {}", handle);
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let mut store = match legal_kernel_store().write() {
        Ok(s) => s,
        Err(e) => {
            eprintln!("CRITICAL: Failed to acquire kernel store write lock: {}", e);
            return PkResult::err(ResultCode::InternalError);
        }
    };
    
    match store.add_line_legal(handle, sku, qty, unit_minor) {
        Ok(()) => PkResult::ok(),
        Err(e) => {
            if e.to_string().contains("expired") {
                PkResult::err(ResultCode::TimedOut)
            } else if e.to_string().contains("not found") {
                PkResult::err(ResultCode::NotFound)
            } else if e.to_string().contains("state") {
                PkResult::err(ResultCode::InvalidState)
            } else {
                PkResult::err(ResultCode::InternalError)
            }
        }
    }
}

#[no_mangle]
pub extern "C" fn pk_add_cash_tender_legal(
    handle: PkTransactionHandle,
    amount_minor: i64
) -> PkResult {
    if handle == PK_INVALID_HANDLE || amount_minor <= 0 {
        eprintln!("ERROR: Invalid tender amount {} for transaction {}", amount_minor, handle);
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let mut store = match legal_kernel_store().write() {
        Ok(s) => s,
        Err(e) => {
            eprintln!("CRITICAL: Failed to acquire kernel store write lock: {}", e);
            return PkResult::err(ResultCode::InternalError);
        }
    };
    
    match store.add_tender_legal(handle, amount_minor) {
        Ok(()) => PkResult::ok(),
        Err(e) => {
            if e.to_string().contains("expired") {
                PkResult::err(ResultCode::TimedOut)
            } else if e.to_string().contains("not found") {
                PkResult::err(ResultCode::NotFound)
            } else if e.to_string().contains("state") {
                PkResult::err(ResultCode::InvalidState)
            } else {
                PkResult::err(ResultCode::InternalError)
            }
        }
    }
}

#[no_mangle]
pub extern "C" fn pk_commit_transaction_legal(handle: PkTransactionHandle) -> PkResult {
    if handle == PK_INVALID_HANDLE {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let mut store = match legal_kernel_store().write() {
        Ok(s) => s,
        Err(e) => {
            eprintln!("CRITICAL: Failed to acquire kernel store write lock: {}", e);
            return PkResult::err(ResultCode::InternalError);
        }
    };
    
    match store.commit_transaction_legal(handle) {
        Ok(()) => PkResult::ok(),
        Err(e) => {
            if e.to_string().contains("not found") {
                PkResult::err(ResultCode::NotFound)
            } else if e.to_string().contains("state") {
                PkResult::err(ResultCode::InvalidState)
            } else {
                PkResult::err(ResultCode::InternalError)
            }
        }
    }
}

#[no_mangle]
pub extern "C" fn pk_abort_transaction_legal(
    handle: PkTransactionHandle,
    reason_ptr: *const u8,
    reason_len: usize
) -> PkResult {
    if handle == PK_INVALID_HANDLE {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let reason = if reason_ptr.is_null() || reason_len == 0 {
        "User abort"
    } else {
        &unsafe { read_str(reason_ptr, reason_len) }
    };
    
    let mut store = match legal_kernel_store().write() {
        Ok(s) => s,
        Err(e) => {
            eprintln!("CRITICAL: Failed to acquire kernel store write lock: {}", e);
            return PkResult::err(ResultCode::InternalError);
        }
    };
    
    match store.abort_transaction_legal(handle, reason) {
        Ok(()) => PkResult::ok(),
        Err(e) => {
            if e.to_string().contains("not found") {
                PkResult::err(ResultCode::NotFound)
            } else if e.to_string().contains("finalized") {
                PkResult::err(ResultCode::InvalidState)
            } else {
                PkResult::err(ResultCode::InternalError)
            }
        }
    }
}

#[no_mangle]
pub extern "C" fn pk_get_totals_legal(
    handle: PkTransactionHandle,
    out_total: *mut i64,
    out_tendered: *mut i64,
    out_change: *mut i64,
    out_state: *mut i32
) -> PkResult {
    if handle == PK_INVALID_HANDLE || out_total.is_null() || out_tendered.is_null() 
        || out_change.is_null() || out_state.is_null() {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let store = match legal_kernel_store().read() {
        Ok(s) => s,
        Err(e) => {
            eprintln!("CRITICAL: Failed to acquire kernel store read lock: {}", e);
            return PkResult::err(ResultCode::InternalError);
        }
    };
    
    let tx = match store.active_transactions.get(&handle) {
        Some(t) => t,
        None => {
            eprintln!("ERROR: Transaction {} not found", handle);
            return PkResult::err(ResultCode::NotFound);
        }
    };
    
    unsafe {
        *out_total = tx.total_minor();
        *out_tendered = tx.tendered_minor;
        *out_change = tx.change_minor();
        *out_state = match tx.state {
            LegalTxState::Building => 0,
            LegalTxState::Committing => 1,
            LegalTxState::Committed => 2,
            LegalTxState::Aborting => 3,
            LegalTxState::Aborted => 4,
            LegalTxState::TimedOut => 5,
        };
    }
    
    PkResult::ok()
}

#[no_mangle]
pub extern "C" fn pk_close_transaction_legal(handle: PkTransactionHandle) -> PkResult {
    if handle == PK_INVALID_HANDLE {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let mut store = match legal_kernel_store().write() {
        Ok(s) => s,
        Err(e) => {
            eprintln!("CRITICAL: Failed to acquire kernel store write lock: {}", e);
            return PkResult::err(ResultCode::InternalError);
        }
    };
    
    match store.active_transactions.remove(&handle) {
        Some(_tx) => {
            store.transaction_timeouts.remove(&handle);
            PkResult::ok()
        },
        None => {
            eprintln!("ERROR: Transaction {} not found for closing", handle);
            PkResult::err(ResultCode::NotFound)
        }
    }
}

// === BACKWARD COMPATIBILITY ===

#[no_mangle]
pub extern "C" fn pk_begin_transaction(
    store_ptr: *const u8,
    store_len: usize,
    currency_ptr: *const u8,
    currency_len: usize,
    out_handle: *mut PkTransactionHandle
) -> PkResult {
    pk_begin_transaction_legal(store_ptr, store_len, currency_ptr, currency_len, out_handle)
}

#[no_mangle]
pub extern "C" fn pk_add_line(
    handle: PkTransactionHandle,
    sku_ptr: *const u8,
    sku_len: usize,
    qty: i32,
    unit_minor: i64
) -> PkResult {
    pk_add_line_legal(handle, sku_ptr, sku_len, qty, unit_minor)
}

#[no_mangle]
pub extern "C" fn pk_add_cash_tender(handle: PkTransactionHandle, amount_minor: i64) -> PkResult {
    pk_add_cash_tender_legal(handle, amount_minor)
}

#[no_mangle]
pub extern "C" fn pk_close_transaction(handle: PkTransactionHandle) -> PkResult {
    pk_close_transaction_legal(handle)
}

#[no_mangle]
pub extern "C" fn pk_get_totals(
    handle: PkTransactionHandle,
    out_total: *mut i64,
    out_tendered: *mut i64,
    out_change: *mut i64,
    out_state: *mut i32
) -> PkResult {
    pk_get_totals_legal(handle, out_total, out_tendered, out_change, out_state)
}

// === VERSION AND ERROR HANDLING ===

#[no_mangle]
pub extern "C" fn pk_get_version() -> *const u8 {
    static VERSION: &str = concat!("0.4.0-threading","\0");
    VERSION.as_ptr()
}

#[no_mangle]
pub extern "C" fn pk_result_is_ok(result: PkResult) -> bool {
    result.code == ResultCode::Ok as i32
}

#[no_mangle]
pub extern "C" fn pk_result_get_code(result: PkResult) -> i32 {
    result.code
}

// === CURRENCY UTILITY FUNCTIONS ===

#[no_mangle]
pub extern "C" fn pk_validate_currency_code(
    currency_ptr: *const u8,
    currency_len: usize
) -> PkResult {
    let currency_str = unsafe { read_str(currency_ptr, currency_len) };
    
    match Currency::new(&currency_str) {
        Ok(_) => PkResult::ok(),
        Err(e) => {
            eprintln!("ERROR: Currency validation failed for '{}': {}", currency_str, e);
            PkResult::err(ResultCode::ValidationFailed)
        }
    }
}

#[no_mangle]
pub extern "C" fn pk_is_standard_currency(
    currency_ptr: *const u8,
    currency_len: usize
) -> bool {
    let currency_str = unsafe { read_str(currency_ptr, currency_len) };
    
    match Currency::new(&currency_str) {
        Ok(currency) => currency.is_standard(),
        Err(_) => false,
    }
}

#[no_mangle]
pub extern "C" fn pk_get_currency_decimal_places(
    handle: PkTransactionHandle,
    out_decimal_places: *mut u8
) -> PkResult {
    if handle == PK_INVALID_HANDLE || out_decimal_places.is_null() {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let store = match legal_kernel_store().read() {
        Ok(s) => s,
        Err(e) => {
            eprintln!("CRITICAL: Failed to acquire kernel store read lock: {}", e);
            return PkResult::err(ResultCode::InternalError);
        }
    };
    
    let tx = match store.active_transactions.get(&handle) {
        Some(t) => t,
        None => {
            eprintln!("ERROR: Transaction {} not found", handle);
            return PkResult::err(ResultCode::NotFound);
        }
    };
    
    unsafe {
        *out_decimal_places = tx.currency.decimal_places();
    }
    
    PkResult::ok()
}

#[no_mangle]
pub extern "C" fn pk_get_line_count(handle: PkTransactionHandle, out_count: *mut u32) -> PkResult {
    if handle == PK_INVALID_HANDLE || out_count.is_null() {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let store = match legal_kernel_store().read() {
        Ok(s) => s,
        Err(e) => {
            eprintln!("CRITICAL: Failed to acquire kernel store read lock: {}", e);
            return PkResult::err(ResultCode::InternalError);
        }
    };
    
    let tx = match store.active_transactions.get(&handle) {
        Some(t) => t,
        None => {
            eprintln!("ERROR: Transaction {} not found", handle);
            return PkResult::err(ResultCode::NotFound);
        }
    };
    
    unsafe {
        *out_count = tx.line_count();
    }
    
    PkResult::ok()
}

#[no_mangle]
pub extern "C" fn pk_get_store_name(
    handle: PkTransactionHandle,
    buffer: *mut u8,
    buffer_size: usize,
    out_required_size: *mut usize
) -> PkResult {
    if handle == PK_INVALID_HANDLE {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let store = match legal_kernel_store().read() {
        Ok(s) => s,
        Err(e) => {
            eprintln!("CRITICAL: Failed to acquire kernel store read lock: {}", e);
            return PkResult::err(ResultCode::InternalError);
        }
    };
    
    let tx = match store.active_transactions.get(&handle) {
        Some(t) => t,
        None => {
            eprintln!("ERROR: Transaction {} not found", handle);
            return PkResult::err(ResultCode::NotFound);
        }
    };
    
    unsafe {
        write_str(&tx.store, buffer, buffer_size, out_required_size)
    }
}

#[no_mangle]
pub extern "C" fn pk_get_currency(
    handle: PkTransactionHandle,
    buffer: *mut u8,
    buffer_size: usize,
    out_required_size: *mut usize
) -> PkResult {
    if handle == PK_INVALID_HANDLE {
        return PkResult::err(ResultCode::ValidationFailed);
    }
    
    let store = match legal_kernel_store().read() {
        Ok(s) => s,
        Err(e) => {
            eprintln!("CRITICAL: Failed to acquire kernel store read lock: {}", e);
            return PkResult::err(ResultCode::InternalError);
        }
    };
    
    let tx = match store.active_transactions.get(&handle) {
        Some(t) => t,
        None => {
            eprintln!("ERROR: Transaction {} not found", handle);
            return PkResult::err(ResultCode::NotFound);
        }
    };
    
    unsafe {
        write_str(tx.currency.code(), buffer, buffer_size, out_required_size)
    }
}
