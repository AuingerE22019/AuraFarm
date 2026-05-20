import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MemberSetup } from './member-setup';

describe('MemberSetup', () => {
  let component: MemberSetup;
  let fixture: ComponentFixture<MemberSetup>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MemberSetup],
    }).compileComponents();

    fixture = TestBed.createComponent(MemberSetup);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
