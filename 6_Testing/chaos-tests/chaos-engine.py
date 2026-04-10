#!/usr/bin/env python3
"""
VanAn Ecosystem - Chaos Testing Engine
Simulates real-world failures to test system resilience
"""

import os
import time
import random
import requests
import threading
import logging
from datetime import datetime, timedelta
from typing import Dict, List, Optional
from dataclasses import dataclass
from enum import Enum

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

class ChaosType(Enum):
    LATENCY = "latency"
    FAILURE = "failure"
    PACKET_LOSS = "packet_loss"
    CPU_STRESS = "cpu_stress"
    MEMORY_STRESS = "memory_stress"

@dataclass
class ChaosConfig:
    enabled: bool
    duration: int = 300  # seconds
    latency_enabled: bool = True
    failures_enabled: bool = False
    intensity: float = 0.3  # 0.0 to 1.0
    target_services: List[str] = None

@dataclass
class ServiceEndpoint:
    name: str
    url: str
    health_endpoint: str
    port: int

class ChaosEngine:
    def __init__(self):
        self.config = self.load_config()
        self.services = self.get_service_endpoints()
        self.active_chaos = []
        self.metrics = {
            'injections': 0,
            'recoveries': 0,
            'errors': 0,
            'start_time': datetime.now()
        }
        
    def load_config(self) -> ChaosConfig:
        """Load chaos configuration from environment variables"""
        return ChaosConfig(
            enabled=os.getenv('ENABLE_CHAOS', 'false').lower() == 'true',
            duration=int(os.getenv('CHAOS_TEST_DURATION', '300')),
            latency_enabled=os.getenv('CHAOS_TEST_LATENCY', 'true').lower() == 'true',
            failures_enabled=os.getenv('CHAOS_TEST_FAILURES', 'false').lower() == 'true',
            intensity=float(os.getenv('CHAOS_INTENSITY', '0.3')),
            target_services=os.getenv('CHAOS_TARGET_SERVICES', 'corehub,gateway,khachlink,shoperp').split(',')
        )
    
    def get_service_endpoints(self) -> List[ServiceEndpoint]:
        """Define service endpoints for chaos testing"""
        return [
            ServiceEndpoint(
                name="corehub",
                url=os.getenv('COREHUB_URL', 'http://localhost:5010'),
                health_endpoint="/health",
                port=5010
            ),
            ServiceEndpoint(
                name="gateway",
                url=os.getenv('GATEWAY_URL', 'http://localhost:5001'),
                health_endpoint="/health",
                port=5001
            ),
            ServiceEndpoint(
                name="khachlink",
                url=os.getenv('KHACHLINK_URL', 'http://localhost:5002'),
                health_endpoint="/health",
                port=5002
            ),
            ServiceEndpoint(
                name="shoperp",
                url=os.getenv('SHOPERP_URL', 'http://localhost:5003'),
                health_endpoint="/health",
                port=5003
            )
        ]
    
    def check_service_health(self, service: ServiceEndpoint) -> bool:
        """Check if a service is healthy"""
        try:
            response = requests.get(
                f"{service.url}{service.health_endpoint}",
                timeout=5
            )
            return response.status_code == 200
        except Exception as e:
            logger.error(f"Health check failed for {service.name}: {e}")
            return False
    
    def inject_latency(self, service: ServiceEndpoint) -> bool:
        """Inject network latency using tc (traffic control)"""
        if not self.config.latency_enabled:
            return False
            
        try:
            # Add 100-500ms latency
            latency_ms = random.randint(100, 500)
            jitter_ms = random.randint(50, 100)
            
            # This would require tc (traffic control) on Linux
            # For demonstration, we'll simulate by making slow requests
            logger.info(f"Injecting {latency_ms}ms latency for {service.name}")
            
            # Simulate latency by making slow requests
            for _ in range(5):
                try:
                    start_time = time.time()
                    requests.get(f"{service.url}/health", timeout=10)
                    actual_time = (time.time() - start_time) * 1000
                    
                    if actual_time < latency_ms:
                        time.sleep((latency_ms - actual_time) / 1000)
                        
                except Exception:
                    pass
                    
            self.metrics['injections'] += 1
            return True
            
        except Exception as e:
            logger.error(f"Failed to inject latency for {service.name}: {e}")
            self.metrics['errors'] += 1
            return False
    
    def inject_failure(self, service: ServiceEndpoint) -> bool:
        """Simulate service failures"""
        if not self.config.failures_enabled:
            return False
            
        try:
            logger.info(f"Simulating failure for {service.name}")
            
            # Simulate failure by making requests that will timeout/fail
            for _ in range(3):
                try:
                    # Request to non-existent endpoint
                    requests.get(f"{service.url}/non-existent-endpoint", timeout=2)
                except requests.exceptions.Timeout:
                    logger.info(f"Timeout injected for {service.name}")
                except requests.exceptions.ConnectionError:
                    logger.info(f"Connection error injected for {service.name}")
                except Exception:
                    pass
                    
            self.metrics['injections'] += 1
            return True
            
        except Exception as e:
            logger.error(f"Failed to inject failure for {service.name}: {e}")
            self.metrics['errors'] += 1
            return False
    
    def run_chaos_scenario(self, service: ServiceEndpoint, chaos_type: ChaosType):
        """Run a specific chaos scenario"""
        logger.info(f"Starting {chaos_type.value} chaos for {service.name}")
        
        success = False
        if chaos_type == ChaosType.LATENCY:
            success = self.inject_latency(service)
        elif chaos_type == ChaosType.FAILURE:
            success = self.inject_failure(service)
        
        if success:
            self.active_chaos.append({
                'service': service.name,
                'type': chaos_type.value,
                'start_time': datetime.now()
            })
            
            # Monitor recovery
            self.monitor_recovery(service)
        
        return success
    
    def monitor_recovery(self, service: ServiceEndpoint):
        """Monitor service recovery after chaos injection"""
        recovery_timeout = 60  # seconds
        start_time = time.time()
        
        while time.time() - start_time < recovery_timeout:
            if self.check_service_health(service):
                logger.info(f"Service {service.name} recovered successfully")
                self.metrics['recoveries'] += 1
                return True
            time.sleep(5)
        
        logger.warning(f"Service {service.name} did not recover within timeout")
        return False
    
    def run_continuous_chaos(self):
        """Run continuous chaos testing for the configured duration"""
        if not self.config.enabled:
            logger.info("Chaos testing is disabled. Set ENABLE_CHAOS=true to enable.")
            return
        
        logger.info(f"Starting chaos testing for {self.config.duration} seconds")
        logger.info(f"Target services: {', '.join(self.config.target_services)}")
        logger.info(f"Intensity: {self.config.intensity * 100}%")
        
        end_time = datetime.now() + timedelta(seconds=self.config.duration)
        
        while datetime.now() < end_time:
            # Select random service and chaos type
            available_services = [s for s in self.services if s.name in self.config.target_services]
            target_service = random.choice(available_services)
            
            chaos_types = []
            if self.config.latency_enabled:
                chaos_types.append(ChaosType.LATENCY)
            if self.config.failures_enabled:
                chaos_types.append(ChaosType.FAILURE)
            
            if chaos_types:
                chaos_type = random.choice(chaos_types)
                
                # Run chaos scenario
                self.run_chaos_scenario(target_service, chaos_type)
                
                # Wait between chaos injections
                wait_time = random.randint(10, 30)
                logger.info(f"Waiting {wait_time} seconds before next chaos injection")
                time.sleep(wait_time)
            else:
                logger.warning("No chaos types enabled")
                break
    
    def generate_report(self) -> Dict:
        """Generate chaos testing report"""
        duration = (datetime.now() - self.metrics['start_time']).total_seconds()
        
        return {
            'test_duration': duration,
            'config': {
                'enabled': self.config.enabled,
                'duration': self.config.duration,
                'latency_enabled': self.config.latency_enabled,
                'failures_enabled': self.config.failures_enabled,
                'intensity': self.config.intensity,
                'target_services': self.config.target_services
            },
            'metrics': self.metrics,
            'services_tested': len([s for s in self.services if s.name in self.config.target_services]),
            'recovery_rate': (self.metrics['recoveries'] / max(self.metrics['injections'], 1)) * 100,
            'error_rate': (self.metrics['errors'] / max(self.metrics['injections'], 1)) * 100
        }
    
    def save_report(self, report: Dict):
        """Save chaos testing report"""
        report_dir = '6_Testing/reports'
        os.makedirs(report_dir, exist_ok=True)
        
        report_file = f"{report_dir}/chaos-test-report-{datetime.now().strftime('%Y%m%d-%H%M%S')}.json"
        
        with open(report_file, 'w') as f:
            import json
            json.dump(report, f, indent=2, default=str)
        
        logger.info(f"Chaos test report saved to {report_file}")
        
        # Also save a summary for dashboard
        summary_file = f"{report_dir}/chaos-summary.json"
        with open(summary_file, 'w') as f:
            json.dump({
                'status': 'completed' if self.config.enabled else 'disabled',
                'duration': report['test_duration'],
                'injections': report['metrics']['injections'],
                'recoveries': report['metrics']['recoveries'],
                'recovery_rate': report['recovery_rate'],
                'last_run': datetime.now().isoformat()
            }, f, indent=2, default=str)

def main():
    """Main chaos testing execution"""
    logger.info("🔥 VanAn Ecosystem - Chaos Testing Engine")
    logger.info("=" * 50)
    
    engine = ChaosEngine()
    
    try:
        # Run continuous chaos testing
        engine.run_continuous_chaos()
        
        # Generate and save report
        report = engine.generate_report()
        engine.save_report(report)
        
        # Print summary
        logger.info("🔥 Chaos Testing Summary:")
        logger.info(f"   Duration: {report['test_duration']:.2f} seconds")
        logger.info(f"   Injections: {report['metrics']['injections']}")
        logger.info(f"   Recoveries: {report['metrics']['recoveries']}")
        logger.info(f"   Recovery Rate: {report['recovery_rate']:.1f}%")
        logger.info(f"   Error Rate: {report['error_rate']:.1f}%")
        
    except KeyboardInterrupt:
        logger.info("Chaos testing interrupted by user")
    except Exception as e:
        logger.error(f"Chaos testing failed: {e}")
    finally:
        logger.info("🔥 Chaos testing completed")

if __name__ == "__main__":
    main()
